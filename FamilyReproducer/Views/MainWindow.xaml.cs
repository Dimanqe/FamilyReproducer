#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using Autodesk.Revit.UI;
using FamilyReproducer.Models;
using FamilyReproducer.Services;
using FamilyReproducer.ViewModels;
using MessageBox = System.Windows.Forms.MessageBox;

#endregion

namespace FamilyReproducer
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _selectedFilePath;

        private MainWindowViewModel _viewModel;
        private UIApplication uiApplication;

        public MainWindow(MainWindowViewModel viewModel, UIApplication uiApplication)
        {
            this.uiApplication = uiApplication;
            _viewModel = viewModel;

            DataContext = viewModel;
            InitializeComponent();
        }

        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            //spContent.Children.Clear();
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Revit Family Files (*.rfa)|*.rfa|All files (*.*)|*.*",
                Title = "Выберите семейство Revit",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _selectedFilePath = openFileDialog.FileName;
                txtFilePath.Text = Path.GetFileName(_selectedFilePath);
                btnReproduce.IsEnabled = true;

                try
                {
                    var extractor = new FamilyDataExtractor(uiApplication);
                    var familyData = extractor.ExtractParameters(_selectedFilePath);

                    if (familyData != null)
                    {
                        _viewModel.FamilyData = familyData;

                        var parameterDisplays = new List<ParameterDisplay>();

                        foreach (var param in familyData.Parameters)
                            parameterDisplays.Add(new ParameterDisplay
                            {
                                Name = param.Name,
                                Type = param.Type,
                                IsInstance = param.IsInstance
                            });

                        ParametersDataGrid.ItemsSource = parameterDisplays;
                    }
                    else
                    {
                        MessageBox.Show("Отсутствуют данные семейства");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось загрузить данные семейства: {ex.Message}", "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}