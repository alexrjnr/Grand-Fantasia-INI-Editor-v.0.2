using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using GrandFantasiaINIEditor.Core;

namespace GrandFantasiaINIEditor.Modules.Collection
{
    public class CollectionEntry : INotifyPropertyChanged
    {
        private string id;
        private string name;
        private bool isBottomWindow;
        private int categoryId;
        private int? subCategoryId;
        private int index;
        private int points;
        private string description;
        private string locateLimit = "";
        private BitmapSource icon;
        private string itemNameFromTItem;
        private bool isSelected;

        public string Id { get => id; set { id = value; OnPropertyChanged(); } }
        public string Name { get => name; set { name = value; OnPropertyChanged(); } }
        public bool IsBottomWindow { get => isBottomWindow; set { isBottomWindow = value; OnPropertyChanged(); } }
        public int CategoryId { get => categoryId; set { categoryId = value; OnPropertyChanged(); } }
        public int? SubCategoryId { get => subCategoryId; set { subCategoryId = value; OnPropertyChanged(); } }
        public int Index { get => index; set { index = value; OnPropertyChanged(); } }
        public int Points { get => points; set { points = value; OnPropertyChanged(); } }
        public string Description { get => description; set { description = value; OnPropertyChanged(); } }
        public string LocateLimit { get => locateLimit; set { locateLimit = value; OnPropertyChanged(); } }

        // UI properties
        public BitmapSource Icon 
        { 
            get => icon; 
            set { icon = value; OnPropertyChanged(); } 
        }
        
        public string ItemNameFromTItem 
        { 
            get => itemNameFromTItem; 
            set { itemNameFromTItem = value; OnPropertyChanged(); } 
        }

        public bool IsSelected
        {
            get => isSelected;
            set { isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class CollectionCategory
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<CollectionSubCategory> SubCategories { get; set; } = new();
        public bool IsExpanded { get; set; }
    }

    public class CollectionSubCategory
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int ParentCategoryId { get; set; }
    }

    public static class CollectionDefinitions
    {
        public static List<CollectionCategory> GetCategories()
        {
            return new List<CollectionCategory>
            {
                new CollectionCategory { Id = 1, Name = "Fantasia", SubCategories = new List<CollectionSubCategory> {
                    new CollectionSubCategory { Id = 1, Name = "Cabeça", ParentCategoryId = 1 },
                    new CollectionSubCategory { Id = 2, Name = "Costas", ParentCategoryId = 1 },
                    new CollectionSubCategory { Id = 3, Name = "Corpo", ParentCategoryId = 1 },
                    new CollectionSubCategory { Id = 4, Name = "Armas", ParentCategoryId = 1 }
                }},
                new CollectionCategory { Id = 2, Name = "Roupa Sprite" },
                new CollectionCategory { Id = 3, Name = "Mobilia" },
                new CollectionCategory { Id = 4, Name = "Montaria" },
                new CollectionCategory { Id = 5, Name = "Outros" },
                new CollectionCategory { Id = 6, Name = "Lendárias", SubCategories = new List<CollectionSubCategory> {
                    new CollectionSubCategory { Id = 1, Name = "Azul", ParentCategoryId = 6 },
                    new CollectionSubCategory { Id = 2, Name = "Amarela", ParentCategoryId = 6 },
                    new CollectionSubCategory { Id = 3, Name = "Roxa", ParentCategoryId = 6 },
                    new CollectionSubCategory { Id = 4, Name = "Vermelha", ParentCategoryId = 6 }
                }},
                new CollectionCategory { Id = 7, Name = "Decor. Ilha" },
                new CollectionCategory { Id = 9, Name = "Trívias" },
                new CollectionCategory { Id = 10, Name = "Tronos" },
                new CollectionCategory { Id = 11, Name = "Album Gourmet" },
                new CollectionCategory { Id = 12, Name = "Viagens", SubCategories = new List<CollectionSubCategory> {
                    new CollectionSubCategory { Id = 1, Name = "Cartão Postal", ParentCategoryId = 12 },
                    new CollectionSubCategory { Id = 2, Name = "Souvenir", ParentCategoryId = 12 }
                }}
            };
        }
    }
}
