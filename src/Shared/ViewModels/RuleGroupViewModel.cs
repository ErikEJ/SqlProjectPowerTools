using System.Collections.ObjectModel;
using System.Linq;
using GalaSoft.MvvmLight;

namespace SqlProjectsPowerTools
{
    public class RuleGroupViewModel : ViewModelBase
    {
        private bool? isEnabled;
        private bool isVisible = true;
        private bool updatingFromRules;

        public RuleGroupViewModel(string groupName, ObservableCollection<RuleViewModel> rules)
        {
            GroupName = groupName;
            Rules = rules;

            foreach (var rule in Rules)
            {
                rule.IsEnabledChanged += (s, e) =>
                {
                    UpdateGroupState();
                    UpdateGroupVisibility();
                };
            }

            UpdateGroupState();
        }

        public string GroupName { get; }

        public ObservableCollection<RuleViewModel> Rules { get; }

        public bool? IsEnabled
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

                if (!updatingFromRules && value.HasValue)
                {
                    SetAllRules(value.Value);
                }
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

        public void UpdateGroupVisibility()
        {
            IsVisible = Rules.Any(r => r.IsVisible);
        }

        private void SetAllRules(bool enabled)
        {
            foreach (var rule in Rules)
            {
                rule.IsEnabled = enabled;
            }
        }

        private void UpdateGroupState()
        {
            updatingFromRules = true;
            try
            {
                var allEnabled = Rules.All(r => r.IsEnabled);
                var allDisabled = Rules.All(r => !r.IsEnabled);
                IsEnabled = allEnabled ? true : (allDisabled ? (bool?)false : null);
            }
            finally
            {
                updatingFromRules = false;
            }
        }
    }
}
