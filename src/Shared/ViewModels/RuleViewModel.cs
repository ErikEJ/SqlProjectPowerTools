using System;
using System.Collections.Generic;
using GalaSoft.MvvmLight;

namespace SqlProjectsPowerTools
{
    public class RuleViewModel : ViewModelBase
    {
        private bool isEnabled;
        private string severity;
        private bool isVisible = true;

        public RuleViewModel(string id, string description, string category, bool enabled, string severity)
        {
            Id = id;
            Description = description;
            Category = category;
            GroupName = GetGroupName(category);
            isEnabled = enabled;
            this.severity = severity;
        }

        public event EventHandler IsEnabledChanged;

        public string Id { get; }

        public string Description { get; }

        public string Category { get; }

        public string GroupName { get; }

        public string DisplayText => $"{Id}: {Description}";

        public IList<string> AvailableSeverities { get; } = new List<string> { "Warning", "Error" };

        public bool IsEnabled
        {
            get => isEnabled;
            set
            {
                if (Equals(value, isEnabled))
                {
                    return;
                }

                isEnabled = value;
                RaisePropertyChanged();
                IsEnabledChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public string Severity
        {
            get => severity;
            set
            {
                if (Equals(value, severity))
                {
                    return;
                }

                severity = value;
                RaisePropertyChanged();
            }
        }

        public bool IsVisible
        {
            get => isVisible;
            set
            {
                if (Equals(value, isVisible))
                {
                    return;
                }

                isVisible = value;
                RaisePropertyChanged();
            }
        }

        private static string GetGroupName(string category)
        {
            if (string.IsNullOrEmpty(category))
            {
                return string.Empty;
            }

            var lastDot = category.LastIndexOf('.');
            return lastDot >= 0 ? category[(lastDot + 1)..] : category;
        }
    }
}
