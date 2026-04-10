using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace GrandFantasiaINIEditor.Modules.Collection
{
    public partial class CollectionBatchAddWindow : Window
    {
        public List<string> ResultIds { get; private set; } = new();
        public string BatchName { get; private set; }
        public string BatchDescription { get; private set; }
        public int BatchPoints { get; private set; }
        public int SelectedCategoryId { get; private set; }
        public int? SelectedSubCategoryId { get; private set; }
        public bool IsBottom { get; private set; }

        private List<CollectionCategory> _allCategories;

        public CollectionBatchAddWindow(List<CollectionCategory> categories, int initialCatId, int? initialSubId)
        {
            InitializeComponent();
            _allCategories = categories;
            CategoryCombo.ItemsSource = _allCategories;

            // Set initial selection
            var cat = _allCategories.FirstOrDefault(c => c.Id == initialCatId);
            if (cat != null)
            {
                CategoryCombo.SelectedItem = cat;
                if (initialSubId.HasValue)
                {
                    var sub = cat.SubCategories?.FirstOrDefault(s => s.Id == initialSubId.Value);
                    if (sub != null) SubCategoryCombo.SelectedItem = sub;
                }
            }

            InputTextBox.Focus();
        }

        private void CategoryCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CategoryCombo.SelectedItem is CollectionCategory cat)
            {
                SubCategoryCombo.ItemsSource = cat.SubCategories ?? new List<CollectionSubCategory>();
                if (cat.SubCategories != null && cat.SubCategories.Count > 0)
                    SubCategoryCombo.SelectedIndex = 0;
                else
                    SubCategoryCombo.SelectedIndex = -1;
            }
            else
            {
                SubCategoryCombo.ItemsSource = null;
                SubCategoryCombo.SelectedIndex = -1;
            }
        }

        private void ProcessButton_Click(object sender, RoutedEventArgs e)
        {
            string input = InputTextBox.Text;
            if (string.IsNullOrWhiteSpace(input))
            {
                MessageBox.Show("Por favor, insira pelo menos um ID.");
                return;
            }

            if (CategoryCombo.SelectedItem is not CollectionCategory cat)
            {
                MessageBox.Show("Por favor, selecione uma categoria.");
                return;
            }

            try
            {
                var ids = new HashSet<string>();
                var parts = input.Split(new[] { ',', ';', ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var part in parts)
                {
                    var cleanPart = part.Trim();
                    if (string.IsNullOrEmpty(cleanPart)) continue;

                    if (cleanPart.Contains("-"))
                    {
                        var range = cleanPart.Split('-');
                        if (range.Length == 2 && int.TryParse(range[0].Trim(), out int start) && int.TryParse(range[1].Trim(), out int end))
                        {
                            int s = Math.Min(start, end);
                            int f = Math.Max(start, end);
                            for (int i = s; i <= f; i++) ids.Add(i.ToString());
                        }
                    }
                    else if (int.TryParse(cleanPart, out int id))
                    {
                        ids.Add(id.ToString());
                    }
                }

                if (ids.Count == 0)
                {
                    MessageBox.Show("Nenhum ID válido encontrado.");
                    return;
                }

                ResultIds = ids.ToList();
                BatchName = BatchNameBox.Text;
                BatchDescription = BatchDescBox.Text;
                BatchPoints = int.TryParse(BatchPointsBox.Text, out int p) ? p : 1;
                SelectedCategoryId = cat.Id;
                SelectedSubCategoryId = (SubCategoryCombo.SelectedItem as CollectionSubCategory)?.Id;
                IsBottom = IsBottomCheck.IsChecked == true;

                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao processar: " + ex.Message);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
