using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HierarchicalMvvm.Demo.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace HierarchicalMvvm.Demo.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private PersonModel? selectedPerson;

        [ObservableProperty]
        private CompanyModel? selectedCompany;

        [ObservableProperty]
        private string changeLog = string.Empty;

        public MainViewModel()
        {
            LoadSampleData();
        }

        private void LoadSampleData()
        {
            // Create simple Person
            var person = new Person("Jan", "Novák", 30, "jan.novak@email.cz");
            SelectedPerson = person.ToModel();
            SelectedPerson.PropertyChanged += OnPersonChanged;

            // Create hierarchical Company structure
            var company = new Company(
                "Acme Corp",
                "123 Main Street",
                new List<Department>
                {
                    new("IT Department", "Alice Johnson", new List<Employee>
                    {
                        new("John", "Doe", 75000, new Person("John", "Doe", 35, "john.doe@acme.com")),
                        new("Jane", "Smith", 80000, new Person("Jane", "Smith", 32, "jane.smith@acme.com"))
                    }),
                    new("HR Department", "Bob Wilson", new List<Employee>
                    {
                        new("Charlie", "Brown", 65000, new Person("Charlie", "Brown", 28, "charlie.brown@acme.com"))
                    })
                }
            );

            SelectedCompany = company.ToModel();
            SelectedCompany.PropertyChanged += OnCompanyChanged;
        }

        private void OnPersonChanged(object? sender, PropertyChangedEventArgs e)
        {
            ChangeLog += $"[{DateTime.Now:HH:mm:ss}] Person.{e.PropertyName} changed\n";
        }

        private void OnCompanyChanged(object? sender, PropertyChangedEventArgs e)
        {
            ChangeLog += $"[{DateTime.Now:HH:mm:ss}] Company.{e.PropertyName} changed in {sender?.GetType().Name}\n";
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
        private void SaveCompany()
        {
            if (SelectedCompany != null)
            {
                var companyRecord = SelectedCompany.ToRecord();
                ChangeLog += $"[{DateTime.Now:HH:mm:ss}] Company saved with {companyRecord.Departments.Count} departments\n";
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

        [RelayCommand]
        private void AddEmployee()
        {
            if (SelectedCompany?.Departments.FirstOrDefault() is DepartmentModel dept)
            {
                var newEmployee = new Employee(
                    "New", 
                    "Employee", 
                    50000, 
                    new Person("New", "Employee", 25, "new.employee@acme.com")
                );
                dept.Employees.Add(newEmployee.ToModel());
                ChangeLog += $"[{DateTime.Now:HH:mm:ss}] Employee added to {dept.Name}\n";
            }
        }

        [RelayCommand]
        private void AddDepartment()
        {
            if (SelectedCompany != null)
            {
                var newDept = new Department(
                    "New Department", 
                    "New Manager", 
                    new List<Employee>()
                );
                SelectedCompany.Departments.Add(newDept.ToModel());
                ChangeLog += $"[{DateTime.Now:HH:mm:ss}] Department added\n";
            }
        }
    }
}