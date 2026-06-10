using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using Poe2Crafter.Core.Matching;
using Poe2Crafter.Core.Models;
using Poe2Crafter.Core.Parsing;

namespace Poe2Crafter.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly ModDatabase _db;
    private readonly CraftMatcher _matcher;

    // ── Slot ──────────────────────────────────────────────────────────
    public IReadOnlyList<SlotOption> SlotOptions { get; } =
        ItemTypeHelper.SlotDisplayNames
            .OrderBy(kv => kv.Value)
            .Select(kv => new SlotOption(kv.Key, kv.Value))
            .ToList();

    private SlotOption _selectedSlotOption;
    public SlotOption SelectedSlotOption
    {
        get => _selectedSlotOption;
        set
        {
            Set(ref _selectedSlotOption, value);
            RefreshBaseOptions();
            RefreshModGroups();
        }
    }

    // ── Armour base ───────────────────────────────────────────────────
    public ObservableCollection<BaseOption> BaseOptions { get; } = [];

    private BaseOption? _selectedBaseOption;
    public BaseOption? SelectedBaseOption
    {
        get => _selectedBaseOption;
        set { Set(ref _selectedBaseOption, value); RefreshModGroups(); }
    }

    private Visibility _baseVisibility = Visibility.Collapsed;
    public Visibility BaseVisibility
    {
        get => _baseVisibility;
        private set => Set(ref _baseVisibility, value);
    }

    // ── Jewel type ────────────────────────────────────────────────────
    public IReadOnlyList<JewelTypeOption> JewelTypeOptions { get; } =
        ItemTypeHelper.JewelTypeDisplayNames
            .OrderBy(kv => kv.Value)
            .Select(kv => new JewelTypeOption(kv.Key, kv.Value))
            .ToList();

    private JewelTypeOption? _selectedJewelType;
    public JewelTypeOption? SelectedJewelType
    {
        get => _selectedJewelType;
        set { Set(ref _selectedJewelType, value); RefreshModGroups(); }
    }

    private Visibility _jewelTypeVisibility = Visibility.Collapsed;
    public Visibility JewelTypeVisibility
    {
        get => _jewelTypeVisibility;
        private set => Set(ref _jewelTypeVisibility, value);
    }

    // ── Mod groups ────────────────────────────────────────────────────
    private string _filter = "";
    public string Filter
    {
        get => _filter;
        set { Set(ref _filter, value); ApplyFilter(); }
    }

    private IReadOnlyList<ModGroup> _allGroups = [];
    public ObservableCollection<ModGroup> FilteredGroups { get; } = [];

    private ModGroup? _selectedGroup;
    public ModGroup? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            Set(ref _selectedGroup, value);
            TierRowVisibility = value is not null ? Visibility.Visible : Visibility.Collapsed;
            RefreshTierOptions();
        }
    }

    private Visibility _tierRowVisibility = Visibility.Collapsed;
    public Visibility TierRowVisibility
    {
        get => _tierRowVisibility;
        private set => Set(ref _tierRowVisibility, value);
    }

    // ── Tier selection ────────────────────────────────────────────────
    public ObservableCollection<ModDefinition> TierOptions { get; } = [];

    private ModDefinition? _selectedTier;
    public ModDefinition? SelectedTier
    {
        get => _selectedTier;
        set { Set(ref _selectedTier, value); AddTargetCommand.Refresh(); }
    }


    // ── Target mods ───────────────────────────────────────────────────
    public ObservableCollection<TargetModViewModel> TargetMods { get; } = [];

    private Visibility _targetListVisibility = Visibility.Collapsed;
    public Visibility TargetListVisibility
    {
        get => _targetListVisibility;
        private set => Set(ref _targetListVisibility, value);
    }

    // ── Status ────────────────────────────────────────────────────────
    private string _statusText = "—";
    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    private Brush _statusBrush = Brushes.Transparent;
    public Brush StatusBrush
    {
        get => _statusBrush;
        private set => Set(ref _statusBrush, value);
    }

    private Visibility _statusVisibility = Visibility.Collapsed;
    public Visibility StatusVisibility
    {
        get => _statusVisibility;
        private set => Set(ref _statusVisibility, value);
    }

    public ObservableCollection<string> MatchedLines { get; } = [];

    // ── Running state ─────────────────────────────────────────────────
    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            Set(ref _isRunning, value);
            Notify(nameof(RunButtonText));
            if (!value)
            {
                StatusVisibility = Visibility.Collapsed;
                IsStop = false;
            }
        }
    }

    private bool _isStop;
    public bool IsStop
    {
        get => _isStop;
        private set => Set(ref _isStop, value);
    }

    public string RunButtonText => IsRunning ? "■  Stop" : "▶  Start";

    public string? LastItemHash { get; private set; }

    private bool _isBlockingEnabled = true;
    public bool IsBlockingEnabled
    {
        get => _isBlockingEnabled;
        set => Set(ref _isBlockingEnabled, value);
    }

    // ── Auto-craft ────────────────────────────────────────────────────
    private bool _isAutoMode;
    public bool IsAutoMode
    {
        get => _isAutoMode;
        set
        {
            Set(ref _isAutoMode, value);
            AutoPanelVisibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private Visibility _autoPanelVisibility = Visibility.Collapsed;
    public Visibility AutoPanelVisibility
    {
        get => _autoPanelVisibility;
        private set => Set(ref _autoPanelVisibility, value);
    }

    private bool _currencySet;
    public bool CurrencySet
    {
        get => _currencySet;
        set => Set(ref _currencySet, value);
    }

    private bool _itemSet;
    public bool ItemSet
    {
        get => _itemSet;
        set => Set(ref _itemSet, value);
    }

    private bool _isCapturing;
    public bool IsCapturing
    {
        get => _isCapturing;
        set => Set(ref _isCapturing, value);
    }

    public RelayCommand SetCurrencyCommand { get; }
    public RelayCommand SetItemCommand     { get; }

    // ── Update ────────────────────────────────────────────────────────
    private string? _updateText;
    public string? UpdateText
    {
        get => _updateText;
        set { Set(ref _updateText, value); Notify(nameof(UpdateVisibility)); }
    }

    public Visibility UpdateVisibility =>
        _updateText is null ? Visibility.Collapsed : Visibility.Visible;

    public RelayCommand UpdateCommand { get; } = new(() => { }); // handled in MainWindow via Executed

    // ── Commands ──────────────────────────────────────────────────────
    public RefreshableCommand AddTargetCommand { get; }
    public RelayCommand<TargetModViewModel> RemoveTargetCommand { get; }
    public RelayCommand ToggleRunningCommand { get; }

    // ─────────────────────────────────────────────────────────────────
    public MainViewModel(ModDatabase db)
    {
        _db      = db;
        _matcher = new CraftMatcher(db);
        AddTargetCommand     = new RefreshableCommand(AddTarget, () => SelectedGroup is not null && SelectedTier is not null);
        RemoveTargetCommand  = new RelayCommand<TargetModViewModel>(RemoveTarget);
        ToggleRunningCommand = new RelayCommand(() => IsRunning = !IsRunning);
        SetCurrencyCommand   = new RelayCommand(() => IsCapturing = true);
        SetItemCommand       = new RelayCommand(() => IsCapturing = true);

        _selectedSlotOption = SlotOptions.First(s => s.Slot == ItemSlot.Ring);
        RefreshBaseOptions();
        RefreshModGroups();
    }

    private void RefreshBaseOptions()
    {
        BaseOptions.Clear();
        var slot = _selectedSlotOption?.Slot ?? ItemSlot.Ring;

        if (ItemTypeHelper.ArmourSlots.Contains(slot))
        {
            foreach (var kv in ItemTypeHelper.ArmourBaseDisplayNames.Where(kv => kv.Key != ArmourBase.None))
                BaseOptions.Add(new BaseOption(kv.Key, kv.Value));
            _selectedBaseOption = BaseOptions.FirstOrDefault();
            BaseVisibility = Visibility.Visible;
            JewelTypeVisibility = Visibility.Collapsed;
        }
        else if (ItemTypeHelper.JewelSlots.Contains(slot))
        {
            _selectedBaseOption = null;
            BaseVisibility = Visibility.Collapsed;
            _selectedJewelType = JewelTypeOptions.FirstOrDefault();
            JewelTypeVisibility = Visibility.Visible;
        }
        else
        {
            _selectedBaseOption = null;
            BaseVisibility = Visibility.Collapsed;
            _selectedJewelType = null;
            JewelTypeVisibility = Visibility.Collapsed;
        }
        Notify(nameof(SelectedBaseOption));
        Notify(nameof(SelectedJewelType));
    }

    private void RefreshModGroups()
    {
        var slot = _selectedSlotOption?.Slot ?? ItemSlot.Ring;
        var armourBase = _selectedBaseOption?.Base ?? ArmourBase.None;
        var jewelType = _selectedJewelType?.Type ?? JewelType.None;
        _allGroups = _db.GetGroups(slot, armourBase, jewelType);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredGroups.Clear();
        var term = Filter.Trim();
        foreach (var g in _allGroups)
            if (term.Length == 0 || g.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase))
                FilteredGroups.Add(g);
        SelectedGroup = null;
    }

    private void RefreshTierOptions()
    {
        TierOptions.Clear();
        SelectedTier = null;
        if (SelectedGroup is null) return;
        foreach (var t in SelectedGroup.Tiers) TierOptions.Add(t);
        SelectedTier = TierOptions.FirstOrDefault();
    }

    private void AddTarget()
    {
        if (SelectedGroup is null || SelectedTier is null) return;
        if (TargetMods.Any(t => t.Group.GroupId == SelectedGroup.GroupId)) return;
        TargetMods.Add(new TargetModViewModel(SelectedGroup, SelectedTier));
        TargetListVisibility = Visibility.Visible;
    }

    private void RemoveTarget(TargetModViewModel t)
    {
        TargetMods.Remove(t);
        if (TargetMods.Count == 0) TargetListVisibility = Visibility.Collapsed;
    }

    public void OnClipboardChanged(string clipboardText)
    {
        var item = ItemParser.TryParse(clipboardText);

        // Update hash even on parse fail — empty hash prevents false matches in AutoCrafter
        LastItemHash = item is { Mods.Count: > 0 }
            ? string.Join("|", item.Mods.Select(m => m.Text))
            : "";

        // Only process clipboard when actively running
        if (!IsRunning || TargetMods.Count == 0)
        {
            StatusVisibility = Visibility.Collapsed;
            IsStop = false;
            return;
        }

        // Ignore partial/unrelated clipboard content — keep showing last known status
        if (item is null) return;

        var conditions = TargetMods.Select(t => t.ToCondition()).ToList();
        var result     = _matcher.Check(item, conditions);

        MatchedLines.Clear();
        StatusVisibility = Visibility.Visible;
        IsStop = result.AllMatched;

        if (result.AllMatched)
        {
            StatusText  = "⛔  STOP";
            StatusBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B));
            foreach (var h in result.Hits)
                MatchedLines.Add($"✓  {h.Target.DisplayName}  T{h.Tier} [{h.Value}]");
        }
        else
        {
            StatusText  = "✓  GO — keep rolling";
            StatusBrush = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
            foreach (var h in result.Hits)
                MatchedLines.Add($"✓  {h.Target.DisplayName}  T{h.Tier} [{h.Value}]");
            foreach (var m in result.Misses)
                MatchedLines.Add($"✗  {m.DisplayName}");
        }
    }

    // Orange info banner (auto-craft stop reasons etc.)
    public void ShowNotice(string message)
    {
        MatchedLines.Clear();
        StatusText       = $"⚠  {message}";
        StatusBrush      = new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22));
        StatusVisibility = Visibility.Visible;
    }

    // ── Settings persistence ──────────────────────────────────────────
    public void ApplySettings(Services.AppSettings s)
    {
        if (Enum.TryParse<ItemSlot>(s.Slot, out var slot))
        {
            var opt = SlotOptions.FirstOrDefault(o => o.Slot == slot);
            if (opt != null) SelectedSlotOption = opt; // triggers base/jewel refresh
        }
        if (Enum.TryParse<ArmourBase>(s.ArmourBase, out var ab))
        {
            var opt = BaseOptions.FirstOrDefault(o => o.Base == ab);
            if (opt != null) SelectedBaseOption = opt;
        }
        if (Enum.TryParse<JewelType>(s.JewelType, out var jt))
        {
            var opt = JewelTypeOptions.FirstOrDefault(o => o.Type == jt);
            if (opt != null) SelectedJewelType = opt;
        }

        var slotNow   = SelectedSlotOption?.Slot ?? ItemSlot.Ring;
        var baseNow   = SelectedBaseOption?.Base ?? ArmourBase.None;
        var jewelNow  = SelectedJewelType?.Type ?? JewelType.None;
        var groups    = _db.GetGroups(slotNow, baseNow, jewelNow);

        foreach (var t in s.Targets)
        {
            var group = groups.FirstOrDefault(g => g.GroupId == t.GroupId);
            var tier  = group?.Tiers.FirstOrDefault(x => x.Tier == t.Tier);
            if (group is null || tier is null) continue;
            if (TargetMods.Any(x => x.Group.GroupId == group.GroupId)) continue;
            TargetMods.Add(new TargetModViewModel(group, tier) { IsExact = t.IsExact });
        }
        if (TargetMods.Count > 0) TargetListVisibility = Visibility.Visible;

        IsBlockingEnabled = s.IsBlockingEnabled;
        IsAutoMode        = s.IsAutoMode;
    }

    public void FillSettings(Services.AppSettings s)
    {
        s.Slot       = SelectedSlotOption?.Slot.ToString();
        s.ArmourBase = SelectedBaseOption?.Base.ToString();
        s.JewelType  = SelectedJewelType?.Type.ToString();
        s.Targets    = TargetMods
            .Select(t => new Services.TargetSetting
            {
                GroupId = t.Group.GroupId,
                Tier    = t.TargetTier.Tier,
                IsExact = t.IsExact,
            })
            .ToList();
        s.IsBlockingEnabled = IsBlockingEnabled;
        s.IsAutoMode        = IsAutoMode;
    }
}
