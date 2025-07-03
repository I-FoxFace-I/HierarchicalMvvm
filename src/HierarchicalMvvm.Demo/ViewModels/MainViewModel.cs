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
        private PersonModel selectedPerson;

        [ObservableProperty]
        private CompanyModel selectedCompany;

        [ObservableProperty]
        private DepartmentModel selectedDepartment;

        [ObservableProperty]
        private EmployeeModel selectedEmployee;

        [ObservableProperty]
        private string changeLog = string.Empty;

        public MainViewModel()
        {
            var initPerson = CreateDefaultPerson();
            var initCompany = CreateDefaultCompany();

            SelectedPerson = initPerson.ToModel();

            SelectedCompany = initCompany.ToModel();

            SelectedDepartment = SelectedCompany.Departments.First();

            SelectedPerson.PropertyChanged += OnPersonChanged;
            SelectedCompany.PropertyChanged += OnCompanyChanged;

            LoadSampleData();
        }

        private Company CreateDefaultCompany()
        {
            return new Company
            {
                Name = "Acme Corp",
                Address = "123 Main Street",
                Departments = new List<Department>
                {
                    new Department
                    {
                        Name = "IT Department",
                        Manager = "Alice Johnson",
                        Employees =  new List<Employee>
                        {
                            new Employee
                            {
                                FirstName = "John",
                                LastName = "Doe",
                                Salary = 75000,
                                PersonalInfo = new Person
                                {
                                    FirstName = "John",
                                    LastName = "Doe",
                                    Age = 35,
                                    Email = "john.doe@acme.com"
                                }
                            },
                            new Employee
                            {
                                FirstName = "Jane",
                                LastName = "Smith",
                                Salary = 80000,
                                PersonalInfo = new Person {
                                    FirstName = "Jane",
                                    LastName = "Smith",
                                    Age = 32,
                                    Email = "jane.smith@acme.com"
                                }
                            }
                        },
                    },
                    new Department
                    {
                        Name = "HR Department",
                        Manager = "Bob Wilson",
                        Employees = new List<Employee>
                        {
                            new Employee
                            {
                                FirstName = "Charlie",
                                LastName = "Brown",
                                Salary = 65000,
                                PersonalInfo = new Person
                                {
                                    FirstName = "Charlie",
                                    LastName = "Brown",
                                    Age = 28,
                                    Email = "charlie.brown@acme.com"
                                }
                            }
                        }
                    }
                }
            };
        }

        private Person CreateDefaultPerson()
        {
            return new Person
            {
                FirstName = "Jan",
                LastName = "Novák",
                Age = 30,
                Email = "jan.novak@email.cz"
            };
        }

        private void LoadSampleData()
        {
            // Create simple Person
            var person = new Person { FirstName = "Jan", LastName = "Novák", Age = 30, Email = "jan.novak@email.cz" };
            SelectedPerson = person.ToModel();
            SelectedPerson.PropertyChanged += OnPersonChanged;

            // Create hierarchical Company structure
            var company = new Company
            {
                Name = "Acme Corp",
                Address = "123 Main Street",
                Departments = new List<Department>
                {
                    new Department
                    {
                        Name = "IT Department",
                        Manager = "Alice Johnson",
                        Employees =  new List<Employee>
                        {
                            new Employee
                            {
                                FirstName = "John",
                                LastName = "Doe",
                                Salary = 75000,
                                PersonalInfo = new Person
                                {
                                    FirstName = "John",
                                    LastName = "Doe",
                                    Age = 35,
                                    Email = "john.doe@acme.com"
                                }
                            },
                            new Employee
                            {
                                FirstName = "Jane",
                                LastName = "Smith",
                                Salary = 80000,
                                PersonalInfo = new Person {
                                    FirstName = "Jane",
                                    LastName = "Smith",
                                    Age = 32,
                                    Email = "jane.smith@acme.com"
                                }
                            }
                        },
                    },
                    new Department
                    {
                        Name = "HR Department",
                        Manager = "Bob Wilson",
                        Employees = new List<Employee>
                        {
                            new Employee
                            {
                                FirstName = "Charlie",
                                LastName = "Brown",
                                Salary = 65000,
                                PersonalInfo = new Person
                                {
                                    FirstName = "Charlie",
                                    LastName = "Brown",
                                    Age = 28,
                                    Email = "charlie.brown@acme.com"
                                }
                            }
                        }
                    }
                }
            };

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
                var originalPerson = new Person
                {
                    FirstName = "Jan",
                    LastName = "Novák",
                    Age = 30,
                    Email = "jan.novak@email.cz"
                };
                SelectedPerson.UpdateFrom(originalPerson);
                ChangeLog += $"[{DateTime.Now:HH:mm:ss}] Person reset\n";
            }
        }

        [RelayCommand]
        private void AddEmployee()
        {

            var newEmployee = new Employee
            {
                FirstName = "New",
                LastName = "Employee",
                Salary = 50000,
                PersonalInfo = new Person { FirstName = "New", LastName = "Employee", Age = 25, Email = "new.employee@acme.com" }
            };

            SelectedCompany.Departments.First().Employees.Add(newEmployee.ToModel());
            ChangeLog += $"[{DateTime.Now:HH:mm:ss}] Employee added to {SelectedDepartment.Name}\n";
        }

        [RelayCommand]
        private void AddDepartment()
        {
            var newDept = new Department
            {
                Name = "New Department",
                Manager = "New Manager",
                Employees = new List<Employee>()
            };

            SelectedCompany.Departments.Add(newDept.ToModel());
            ChangeLog += $"[{DateTime.Now:HH:mm:ss}] Department added\n";
        }
    }
}