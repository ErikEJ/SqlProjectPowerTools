using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using DacFXToolLib.Common;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;

namespace SqlProjectsPowerTools
{
    public class ManageRulesViewModel : ViewModelBase
    {
        private bool runSqlCodeAnalysis;
        private string searchText = string.Empty;
        private string selectedSeverityFilter;
        private bool hasWildcards;
        private bool hasRulesPackages;
        private bool installRulesPackages;

        public ManageRulesViewModel()
        {
            OkCommand = new RelayCommand(OkExecuted);
            CancelCommand = new RelayCommand(CancelExecuted);
            ResetCommand = new RelayCommand(ResetExecuted, () => !HasWildcards);
            SeverityFilters = new List<string> { "All severities", "Warning", "Error" };
            selectedSeverityFilter = SeverityFilters[0];
        }

        public event EventHandler<CloseRequestedEventArgs> CloseRequested;

        public ICommand OkCommand { get; }

        public ICommand CancelCommand { get; }

        public ICommand ResetCommand { get; }

        public Func<bool> ConfirmReset { get; set; }

        public ObservableCollection<RuleGroupViewModel> Groups { get; } = new ObservableCollection<RuleGroupViewModel>();

        public IList<string> SeverityFilters { get; }

        public bool RunSqlCodeAnalysis
        {
            get => runSqlCodeAnalysis;
            set
            {
                if (Equals(value, runSqlCodeAnalysis))
                {
                    return;
                }

                runSqlCodeAnalysis = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(ShowNoRulesPackageWarning));
                RaisePropertyChanged(nameof(CanInstallRulesPackages));
                if (!CanInstallRulesPackages)
                {
                    InstallRulesPackages = false;
                }
            }
        }

        public string SearchText
        {
            get => searchText;
            set
            {
                if (Equals(value, searchText))
                {
                    return;
                }

                searchText = value;
                RaisePropertyChanged();
                ApplyFilter();
            }
        }

        public string SelectedSeverityFilter
        {
            get => selectedSeverityFilter;
            set
            {
                if (Equals(value, selectedSeverityFilter))
                {
                    return;
                }

                selectedSeverityFilter = value;
                RaisePropertyChanged();
                ApplyFilter();
            }
        }

        public bool HasWildcards
        {
            get => hasWildcards;
            private set
            {
                if (Equals(value, hasWildcards))
                {
                    return;
                }

                hasWildcards = value;
                RaisePropertyChanged();
                ((RelayCommand)ResetCommand).RaiseCanExecuteChanged();
            }
        }

        public bool HasRulesPackages
        {
            get => hasRulesPackages;
            private set
            {
                if (Equals(value, hasRulesPackages))
                {
                    return;
                }

                hasRulesPackages = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(ShowNoRulesPackageWarning));
                RaisePropertyChanged(nameof(CanInstallRulesPackages));
                if (!CanInstallRulesPackages)
                {
                    InstallRulesPackages = false;
                }
            }
        }

        public bool ShowNoRulesPackageWarning => RunSqlCodeAnalysis && !HasRulesPackages;

        public bool InstallRulesPackages
        {
            get => installRulesPackages;
            set
            {
                if (Equals(value, installRulesPackages))
                {
                    return;
                }

                installRulesPackages = value;
                RaisePropertyChanged();
            }
        }

        public bool CanInstallRulesPackages => RunSqlCodeAnalysis && !HasRulesPackages;

        public void LoadRules(IList<IssueTypeModel> rules, bool runCodeAnalysis, string rulesExpression, bool hasRulesPackages)
        {
            HasWildcards = rulesExpression?.Contains('*') ?? false;

            runSqlCodeAnalysis = runCodeAnalysis;
            RaisePropertyChanged(nameof(RunSqlCodeAnalysis));

            installRulesPackages = false;
            RaisePropertyChanged(nameof(InstallRulesPackages));

            HasRulesPackages = hasRulesPackages;

            Groups.Clear();

            var grouped = rules.GroupBy(r => r.Category);
            foreach (var group in grouped.OrderBy(g => g.Key))
            {
                var ruleVms = group
                    .OrderBy(r => r.Id)
                    .Select(r => new RuleViewModel(
                        r.Id,
                        r.Description,
                        r.Category,
                        r.Enabled,
                        NormalizeSeverity(r.Severity)))
                    .ToList();

                var groupName = ruleVms.Count > 0 ? ruleVms[0].GroupName : group.Key;
                Groups.Add(new RuleGroupViewModel(groupName, new ObservableCollection<RuleViewModel>(ruleVms)));
            }
        }

        public (bool RunCodeAnalysis, string RulesExpression, bool AddRulesPackages) GetResult()
        {
            var parts = new List<string>();
            foreach (var group in Groups)
            {
                foreach (var rule in group.Rules)
                {
                    if (!rule.IsEnabled)
                    {
                        parts.Add($"-{rule.Id}");
                    }
                    else if (string.Equals(rule.Severity, "Error", StringComparison.OrdinalIgnoreCase))
                    {
                        parts.Add($"+!{rule.Id}");
                    }
                }
            }

            return (RunSqlCodeAnalysis, string.Join(";", parts), InstallRulesPackages);
        }

        private static string NormalizeSeverity(string severity)
        {
            return string.Equals(severity, "Error", StringComparison.OrdinalIgnoreCase) ? "Error" : "Warning";
        }

        private void ApplyFilter()
        {
            var search = searchText?.Trim() ?? string.Empty;
            var severityFilter = selectedSeverityFilter == "All severities" ? null : selectedSeverityFilter;

            foreach (var group in Groups)
            {
                foreach (var rule in group.Rules)
                {
                    var matchesSearch = string.IsNullOrEmpty(search)
                        || rule.Id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                        || rule.Description.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                        || rule.Category.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

                    var matchesSeverity = severityFilter == null
                        || string.Equals(rule.Severity, severityFilter, StringComparison.OrdinalIgnoreCase);

                    rule.IsVisible = matchesSearch && matchesSeverity;
                }

                group.UpdateGroupVisibility();
            }
        }

        private void ResetExecuted()
        {
            if (ConfirmReset?.Invoke() != true)
            {
                return;
            }

            foreach (var group in Groups)
            {
                foreach (var rule in group.Rules)
                {
                    rule.IsEnabled = true;
                    rule.Severity = "Warning";
                }
            }

            ApplyFilter();
        }

        private void OkExecuted()
        {
            CloseRequested?.Invoke(this, new CloseRequestedEventArgs(true));
        }

        private void CancelExecuted()
        {
            CloseRequested?.Invoke(this, new CloseRequestedEventArgs(false));
        }
    }
}
