using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace GrandFantasiaINIEditor.Modules.DropItem
{
    public class DropEntry : INotifyPropertyChanged
    {
        private string id;
        private string name;
        private BitmapSource icon;

        public string Id { get => id; set { id = value; OnPropertyChanged(); } }
        public string Name { get => name; set { name = value; OnPropertyChanged(); } }
        public BitmapSource Icon { get => icon; set { icon = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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
