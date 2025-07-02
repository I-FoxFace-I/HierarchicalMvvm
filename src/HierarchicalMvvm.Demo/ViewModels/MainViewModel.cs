using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HierarchicalMvvm.Demo.Models;
using System;
using System.ComponentModel;

namespace HierarchicalMvvm.Demo.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private PersonModel? selectedPerson;

        [ObservableProperty]
        private string changeLog = string.Empty;

        public MainViewModel()
        {
            LoadSampleData();
        }

        private void LoadSampleData()
        {
            var person = new Person("Jan", "Novák", 30, "jan.novak@email.cz");
            SelectedPerson = person.ToModel();
            
            // Subscribe k change events
            SelectedPerson.PropertyChanged += OnPersonChanged;
        }

        private void OnPersonChanged(object? sender, PropertyChangedEventArgs e)
        {
            ChangeLog += $"[{DateTime.Now:HH:mm:ss}] {e.PropertyName} changed\n";
        }

        [RelayCommand]
        private void SavePerson()
        {
            if (SelectedPerson != null)
            {
                var personRecord = SelectedPerson.ToRecord();
                ChangeLog += $"[{DateTime.Now:HH:mm:ss}] Person saved: {personRecord}\n";
            }
        }

        [RelayCommand]
        private void ResetPerson()
        {
            if (SelectedPerson != null)
            {
                var originalPerson = new Person("Jan", "Novák", 30, "jan.novak@email.cz");
                SelectedPerson.UpdateFrom(originalPerson);
                ChangeLog += $"[{DateTime.Now:HH:mm:ss}] Person reset\n";
            }
        }
    }
}