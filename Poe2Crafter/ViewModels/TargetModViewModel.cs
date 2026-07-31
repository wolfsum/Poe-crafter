using Poe2Crafter.Core.Matching;
using Poe2Crafter.Core.Models;

namespace Poe2Crafter.ViewModels;

public class TargetModViewModel : ViewModelBase
{
    public ModGroup Group { get; }
    public ModDefinition TargetTier { get; }

    private bool _isExact;
    public bool IsExact
    {
        get => _isExact;
        set { Set(ref _isExact, value); Notify(nameof(DisplayText)); }
    }

    public TargetModViewModel(ModGroup group, ModDefinition tier)
    {
        Group      = group;
        TargetTier = tier;
    }

    public string DisplayText
    {
        get
        {
            var op    = IsExact ? "=" : "≥";
            var range = TargetTier.ValuesMin.Length > 0
                ? $" [{TargetTier.ValuesMin[0]}-{TargetTier.ValuesMax[0]}]"
                : "";
            return $"{Group.DisplayName}  {op} T{TargetTier.Tier}{range}";
        }
    }

    public TargetCondition ToCondition() =>
        new(Group.GroupId, TargetTier.Tier, Group.DisplayName,
            IsExact ? TierMatchMode.Exact : TierMatchMode.AtLeast,
            Group.ModType);
}
