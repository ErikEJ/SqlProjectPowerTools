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
            GroupName = GetGroupName(id, category);
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

        private static string GetGroupName(string id, string category)
        {
            var lastDot = category?.LastIndexOf('.') ?? -1;
            var baseName = string.IsNullOrEmpty(category)
                ? string.Empty
                : (lastDot >= 0 ? category.Substring(lastDot + 1) : category);

            if (string.IsNullOrEmpty(id))
            {
                return baseName;
            }

            var parts = id.Split('.');
            var suffix = parts.Length >= 4
                ? $"({parts[0]})"
                : parts.Length == 3
                    ? $"({parts[0]}.{parts[1]})"
                    : parts.Length == 2
                        ? $"({parts[0]})"
                        : string.Empty;

            return string.IsNullOrEmpty(suffix) ? baseName : $"{baseName} {suffix}";
        }
    }
}
