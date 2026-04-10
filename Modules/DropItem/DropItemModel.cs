using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace GrandFantasiaINIEditor.Modules.DropItem
{
    public class DropEntry
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public BitmapSource Icon { get; set; }
    }

    public class DropItemSlot : INotifyPropertyChanged
    {
        private string _itemId;
        private string _itemName;
        private BitmapSource _icon;
        private string _stack;
        private string _rate;

        public int Index { get; set; }

        public string ItemId
        {
            get => _itemId;
            set
            {
                if (_itemId != value)
                {
                    _itemId = value;
                    OnPropertyChanged();
                    OnIdChanged?.Invoke(this, value);
                }
            }
        }

        public string ItemName
        {
            get => _itemName;
            set { _itemName = value; OnPropertyChanged(); }
        }

        public string Stack
        {
            get => _stack;
            set { _stack = value; OnPropertyChanged(); }
        }

        public string Rate
        {
            get => _rate;
            set { _rate = value; OnPropertyChanged(); }
        }

        public BitmapSource Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(); }
        }

        public Action<DropItemSlot, string> OnIdChanged { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
