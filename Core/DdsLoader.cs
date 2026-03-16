using Pfim;
using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GrandFantasiaINIEditor.Core
{
    /// <summary>
    /// Loads DDS textures into frozen WPF BitmapSources.
    ///
    /// For uncompressed 16-bit DDS files (FourCC = 0, BitCount = 16) we parse the
    /// DDS pixel-format bitmasks directly from the header and convert to BGRA32
    /// ourselves, regardless of which 16-bit variant the file uses
    /// (R5G6B5, A1R5G5B5, A4R4G4B4, X1R5G5B5, …).
    ///
    /// For all other formats (DXT1/3/5 compressed, 32-bit uncompressed, …)
    /// we delegate to Pfimage as before.
    /// </summary>
    public static class DdsLoader
    {
        // ── DDS header offsets (all values are little-endian uint32) ─────────
        private const int OFF_MAGIC          = 0;  // "DDS "
        private const int OFF_PF_FLAGS       = 80; // DDSPIXELFORMAT.dwFlags
        private const int OFF_PF_FOURCC      = 84; // DDSPIXELFORMAT.dwFourCC
        private const int OFF_PF_BITCOUNT    = 88; // DDSPIXELFORMAT.dwRGBBitCount
        private const int OFF_PF_R_MASK      = 92; // DDSPIXELFORMAT.dwRBitMask
        private const int OFF_PF_G_MASK      = 96; // DDSPIXELFORMAT.dwGBitMask
        private const int OFF_PF_B_MASK      = 100;// DDSPIXELFORMAT.dwBBitMask
        private const int OFF_PF_A_MASK      = 104;// DDSPIXELFORMAT.dwABitMask
        private const int DDS_HEADER_SIZE    = 128;// header + reserved = 128 bytes

        private const uint DDSPF_RGB  = 0x40;
        private const uint DDSPF_RGBA = 0x41;

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Returns a frozen BitmapSource, or null on failure.</summary>
        public static BitmapSource Load(string ddsPath)
        {
            if (string.IsNullOrWhiteSpace(ddsPath) || !File.Exists(ddsPath))
                return null;

            try
            {
                byte[] raw = File.ReadAllBytes(ddsPath);

                // Validate magic
                if (raw.Length < DDS_HEADER_SIZE ||
                    raw[0] != 'D' || raw[1] != 'D' || raw[2] != 'S' || raw[3] != ' ')
                    return null;

                uint fourCC  = ReadUInt32(raw, OFF_PF_FOURCC);
                uint bitCount = ReadUInt32(raw, OFF_PF_BITCOUNT);

                // Uncompressed 16-bit: handle ourselves with the actual bitmasks
                if (fourCC == 0 && bitCount == 16)
                {
                    uint rMask = ReadUInt32(raw, OFF_PF_R_MASK);
                    uint gMask = ReadUInt32(raw, OFF_PF_G_MASK);
                    uint bMask = ReadUInt32(raw, OFF_PF_B_MASK);
                    uint aMask = ReadUInt32(raw, OFF_PF_A_MASK);

                    // Parse width/height from header (offsets 16 and 12)
                    int width  = (int)ReadUInt32(raw, 16);
                    int height = (int)ReadUInt32(raw, 12);

                    return Convert16BitToBgra32(raw, DDS_HEADER_SIZE,
                                                width, height,
                                                rMask, gMask, bMask, aMask);
                }

                // All other formats: delegate to Pfimage
                using var image = Pfimage.FromFile(ddsPath);

                var bitmap = BitmapSource.Create(
                    image.Width, image.Height,
                    96, 96,
                    PixelFormats.Bgra32,
                    null,
                    image.Data,
                    image.Stride);

                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private static uint ReadUInt32(byte[] buf, int offset) =>
            (uint)(buf[offset] | (buf[offset + 1] << 8) |
                   (buf[offset + 2] << 16) | (buf[offset + 3] << 24));

        /// <summary>
        /// Converts arbitrary 16-bit uncompressed DDS pixels to BGRA32 using the
        /// actual channel bitmasks from the DDS PixelFormat header.
        /// Works for R5G6B5, A1R5G5B5, X1R5G5B5, A4R4G4B4, and any other variant.
        /// </summary>
        private static BitmapSource Convert16BitToBgra32(
            byte[] raw, int dataOffset,
            int width, int height,
            uint rMask, uint gMask, uint bMask, uint aMask)
        {
            byte[] dst    = new byte[width * height * 4];
            int    stride = width * 4;
            int    si     = dataOffset;
            int    di     = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (si + 1 >= raw.Length) break;

                    ushort px = (ushort)(raw[si] | (raw[si + 1] << 8));
                    si += 2;

                    byte r = ExtractChannel(px, rMask);
                    byte g = ExtractChannel(px, gMask);
                    byte b = ExtractChannel(px, bMask);
                    byte a = aMask != 0 ? ExtractChannel(px, aMask) : (byte)255;

                    // WPF Bgra32: B G R A
                    dst[di++] = b;
                    dst[di++] = g;
                    dst[di++] = r;
                    dst[di++] = a;
                }
            }

            var bitmap = BitmapSource.Create(width, height, 96, 96,
                PixelFormats.Bgra32, null, dst, stride);
            bitmap.Freeze();
            return bitmap;
        }

        /// <summary>
        /// Extracts a channel from a 16-bit pixel using a bitmask, then scales
        /// the result to the full 8-bit (0-255) range.
        /// </summary>
        private static byte ExtractChannel(ushort px, uint mask)
        {
            if (mask == 0) return 0;

            // Find the lowest set bit of the mask (shift amount)
            int shift = 0;
            uint m = mask;
            while ((m & 1) == 0) { m >>= 1; shift++; }

            // Number of bits in this channel
            uint value = (px & mask) >> shift;
            int bits = 0;
            uint tmp = m;
            while (tmp != 0) { tmp >>= 1; bits++; }

            if (bits == 0) return 0;

            // Scale from `bits` bits to 8 bits
            // Use bit-replication for accuracy (same as GPU hardware does)
            int scaled = (int)(value << (8 - bits));
            scaled |= scaled >> bits; // replicate top bits into lower positions
            return (byte)Math.Min(255, scaled);
        }
    }
}
