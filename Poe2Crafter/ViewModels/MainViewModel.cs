using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using Poe2Crafter.Core.Games;
using Poe2Crafter.Core.Matching;
using Poe2Crafter.Core.Models;
using Poe2Crafter.Core.Parsing;

namespace Poe2Crafter.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly ModDatabase _db;
    private readonly CraftMatcher _matcher;
    private readonly GameProfile _profile;

    public string GameName => _profile.Name;
    public string GameKey  => _profile.Key;

    // ── Slot ──────────────────────────────────────────────────────────
    public IReadOnlyList<SlotOption> SlotOptions { get; }

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
    public ObservableCollection<JewelTypeOption> JewelTypeOptions { get; } = [];

    private JewelTypeOption? _selectedJewelType;
    public JewelTypeOption? SelectedJewelType
    {
        get => _selectedJewelType;
        set { Set(ref _selectedJewelType, value); RefreshClusterBases(); RefreshModGroups(); }
    }

    // ── Cluster base (PoE1) ───────────────────────────────────────────
    public ObservableCollection<ClusterBase> ClusterBaseOptions { get; } = [];

    private ClusterBase? _selectedClusterBase;
    public ClusterBase? SelectedClusterBase
    {
        get => _selectedClusterBase;
        set { Set(ref _selectedClusterBase, value); RefreshModGroups(); }
    }

    private Visibility _clusterBaseVisibility = Visibility.Collapsed;
    public Visibility ClusterBaseVisibility
    {
        get => _clusterBaseVisibility;
        private set => Set(ref _clusterBaseVisibility, value);
    }

    private Visibility _jewelTypeVisibility = Visibility.Collapsed;
    public Visibility JewelTypeVisibility
    {
        get => _jewelTypeVisibility;
        private set => Set(ref _jewelTypeVisibility, value);
    }

    // ── Tablet type ───────────────────────────────────────────────────
    public IReadOnlyList<TabletTypeOption> TabletTypeOptions { get; }

    private TabletTypeOption? _selectedTabletType;
    public TabletTypeOption? SelectedTabletType
    {
        get => _selectedTabletType;
        set { Set(ref _selectedTabletType, value); RefreshModGroups(); }
    }

    private Visibility _tabletTypeVisibility = Visibility.Collapsed;
    public Visibility TabletTypeVisibility
    {
        get => _tabletTypeVisibility;
        private set => Set(ref _tabletTypeVisibility, value);
    }

    // ── Influence (PoE1) ──────────────────────────────────────────────
    public IReadOnlyList<InfluenceOption> InfluenceOptions { get; }

    private InfluenceOption? _selectedInfluence;
    public InfluenceOption? SelectedInfluence
    {
        get => _selectedInfluence;
        set { Set(ref _selectedInfluence, value); RefreshModGroups(); }
    }

    private Visibility _influenceVisibility = Visibility.Collapsed;
    public Visibility InfluenceVisibility
    {
        get => _influenceVisibility;
        private set => Set(ref _influenceVisibility, value);
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
            bool wasRunning = _isRunning;
            Set(ref _isRunning, value);
            Notify(nameof(RunButtonText));
            if (value)
            {
                if (!wasRunning) SessionSpent = 0; // every Start opens a fresh tally
            }
            else
            {
                StatusVisibility = Visibility.Collapsed;
                IsStop = false;
                AutoProgress = "";
                _applyPending = false;
            }
        }
    }

    // ── Spend counter ─────────────────────────────────────────────────
    // An "apply" is registered when currency is actually used (auto-crafter
    // click, or a live click that passed the hook), but only counted once the
    // following clipboard read yields a readable item — a click on the stash
    // background or a dropped keystroke must not inflate the tally. The tally
    // spans the whole session, including a multi-item queue: the budget is what
    // the stack can afford, not what one item costs.
    private volatile bool _applyPending;

    private int _sessionSpent;
    public int SessionSpent
    {
        get => _sessionSpent;
        private set { Set(ref _sessionSpent, value); Notify(nameof(SpendText)); Notify(nameof(SpendTooltip)); }
    }

    private int _totalSpent;
    public int TotalSpent
    {
        get => _totalSpent;
        set { Set(ref _totalSpent, value); Notify(nameof(SpendTooltip)); }
    }

    // 0 = unlimited. Any positive value stops the session once that many orbs
    // have been spent, so an auto-craft left alone can't eat the whole stack.
    private int _stopAfter;
    public int StopAfter
    {
        get => _stopAfter;
        set { Set(ref _stopAfter, value); Notify(nameof(StopAfterText)); Notify(nameof(SpendText)); }
    }

    public string StopAfterText
    {
        get => _stopAfter.ToString();
        set => StopAfter = int.TryParse(value, out var n) && n > 0 ? n : 0;
    }

    public string SpendText => StopAfter > 0 ? $"{SessionSpent} / {StopAfter}" : SessionSpent.ToString();

    public string SpendTooltip =>
        $"Потрачено за сессию: {SessionSpent}. Всего: {TotalSpent}. Лимит 0 = без ограничения.";

    // Raised when the spend limit is reached — the window owns stopping, since
    // it has to stop the crafter and the mouse hook as well.
    public event Action<string>? StopRequested;

    // Called from the auto-crafter thread and from the mouse hook
    public void RegisterApply() => _applyPending = true;

    private bool _isStop;
    public bool IsStop
    {
        get => _isStop;
        private set
        {
            bool wasStop = _isStop;
            Set(ref _isStop, value);
            // Audible alert on the GO→STOP transition. While crafting you watch
            // the game, not this window — the red STOP panel is out of view, so
            // sound is the only feedback that reaches you. Fired once per hit,
            // not on every clipboard eval while already stopped.
            if (value && !wasStop) PlayStopAlert();
        }
    }

    private static void PlayStopAlert() => Task.Run(() =>
    {
        try
        {
            // Two crisp high beeps — cuts through game audio
            Console.Beep(1200, 150);
            Console.Beep(1600, 250);
        }
        catch { /* no console/audio device — never break crafting over a beep */ }
    });

    public string RunButtonText => IsRunning ? "■  Stop" : "▶  Start";

    public string? LastItemHash { get; private set; }

    // Bumped after every processed clipboard event — AutoCrafter syncs on this
    // instead of fixed delays (PoE2 fires several clipboard events per copy)
    private int _evalSeq;
    public int EvalSeq => _evalSeq;

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

    private bool _augCurrencySet;
    public bool AugCurrencySet
    {
        get => _augCurrencySet;
        set => Set(ref _augCurrencySet, value);
    }

    // Chaos/Alt = spam one currency. Alt+Aug = Alt rerolls, Aug fills empty side.
    private bool _isAltAugMode;
    public bool IsAltAugMode
    {
        get => _isAltAugMode;
        set
        {
            if (_isAltAugMode == value) return;
            Set(ref _isAltAugMode, value);
            Notify(nameof(IsChaosAltMode));
            Notify(nameof(CurrencyButtonLabel));
            AugPanelVisibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public bool IsChaosAltMode
    {
        get => !_isAltAugMode;
        set { if (value) IsAltAugMode = false; }
    }

    public string CurrencyButtonLabel => _isAltAugMode ? "Set Alt" : "Set Currency";

    private Visibility _augPanelVisibility = Visibility.Collapsed;
    public Visibility AugPanelVisibility
    {
        get => _augPanelVisibility;
        private set => Set(ref _augPanelVisibility, value);
    }

    // Next orb after the latest clipboard eval — AutoCrafter reads this.
    public CraftAction LastCraftAction { get; private set; } = CraftAction.UseAlt;

    // ── Item queue ────────────────────────────────────────────────────
    public const int MinItems = 1;
    public const int MaxItems = 10;

    // One entry per item to craft. The crafter rolls them in order, advancing
    // to the next once a slot's targets are hit.
    public ObservableCollection<ItemSlotViewModel> ItemSlots { get; } = [];

    private int _itemCount = 1;
    public int ItemCount
    {
        get => _itemCount;
        set
        {
            int clamped = Math.Clamp(value, MinItems, MaxItems);
            if (clamped == _itemCount && ItemSlots.Count == clamped) return;
            Set(ref _itemCount, clamped);
            SyncItemSlots();
            IncItemsCommand.Refresh();
            DecItemsCommand.Refresh();
        }
    }

    // Grow/shrink the slot list to match ItemCount. New slots start unset;
    // shrinking drops from the end so existing numbers/positions are kept.
    private void SyncItemSlots()
    {
        while (ItemSlots.Count < _itemCount)
        {
            var slot = new ItemSlotViewModel(ItemSlots.Count + 1);
            slot.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ItemSlotViewModel.IsSet)) Notify(nameof(AllItemsSet));
            };
            ItemSlots.Add(slot);
        }
        while (ItemSlots.Count > _itemCount)
            ItemSlots.RemoveAt(ItemSlots.Count - 1);
        Notify(nameof(AllItemsSet));
    }

    // Every queued slot has a captured position — required before auto-craft.
    public bool AllItemsSet => ItemSlots.Count > 0 && ItemSlots.All(s => s.IsSet);

    private bool _isCapturing;
    public bool IsCapturing
    {
        get => _isCapturing;
        set => Set(ref _isCapturing, value);
    }

    // Where the next in-game click lands: Alt/currency, Aug, or a specific item
    // slot. MainWindow reads these when a position is captured.
    public bool CapturingCurrency { get; private set; }
    public bool CapturingAug      { get; private set; }
    public ItemSlotViewModel? CapturingSlot { get; private set; }

    // Progress banner while crafting a multi-item queue ("Крафчу 2 / 4").
    private string _autoProgress = "";
    public string AutoProgress
    {
        get => _autoProgress;
        set => Set(ref _autoProgress, value);
    }

    public RelayCommand SetCurrencyCommand { get; }
    public RelayCommand SetAugCurrencyCommand { get; }
    public RelayCommand<ItemSlotViewModel> SetItemSlotCommand { get; }
    public RefreshableCommand IncItemsCommand { get; }
    public RefreshableCommand DecItemsCommand { get; }

    // ── Update ────────────────────────────────────────────────────────
    public string VersionText { get; } =
        $"v{Services.UpdateService.Current.ToString(3)}";

    private string _updateText = "⟳ Check";
    public string UpdateText
    {
        get => _updateText;
        set => Set(ref _updateText, value);
    }

    public RelayCommand UpdateCommand { get; } = new(() => { }); // handled in MainWindow via Executed
    public RelayCommand SwitchGameCommand { get; } = new(() => { }); // handled in MainWindow via Executed

    // ── Commands ──────────────────────────────────────────────────────
    public RefreshableCommand AddTargetCommand { get; }
    public RelayCommand<TargetModViewModel> RemoveTargetCommand { get; }
    public RelayCommand ToggleRunningCommand { get; }

    // ─────────────────────────────────────────────────────────────────
    public MainViewModel(ModDatabase db, GameProfile profile)
    {
        _db      = db;
        _profile = profile;
        _matcher = new CraftMatcher(db, profile.UseAnnotationTiers);

        SlotOptions = profile.Slots
            .Select(s => new SlotOption(s, profile.SlotDisplayNames.GetValueOrDefault(s, s.ToString())))
            .OrderBy(o => o.Name)
            .ToList();
        TabletTypeOptions = profile.TabletTypeDisplayNames
            .OrderBy(kv => kv.Value)
            .Select(kv => new TabletTypeOption(kv.Key, kv.Value)).ToList();
        InfluenceOptions = profile.InfluenceDisplayNames
            .Select(kv => new InfluenceOption(kv.Key, kv.Value)).ToList();

        AddTargetCommand     = new RefreshableCommand(AddTarget, () => SelectedGroup is not null && SelectedTier is not null);
        RemoveTargetCommand  = new RelayCommand<TargetModViewModel>(RemoveTarget);
        ToggleRunningCommand = new RelayCommand(() => IsRunning = !IsRunning);
        SetCurrencyCommand = new RelayCommand(() =>
        {
            CapturingCurrency = true;
            CapturingAug      = false;
            CapturingSlot     = null;
            IsCapturing       = true;
        });
        SetAugCurrencyCommand = new RelayCommand(() =>
        {
            CapturingCurrency = false;
            CapturingAug      = true;
            CapturingSlot     = null;
            IsCapturing       = true;
        });
        SetItemSlotCommand = new RelayCommand<ItemSlotViewModel>(slot =>
        {
            CapturingCurrency = false;
            CapturingAug      = false;
            CapturingSlot     = slot;
            IsCapturing       = true;
        });
        IncItemsCommand = new RefreshableCommand(() => ItemCount++, () => ItemCount < MaxItems);
        DecItemsCommand = new RefreshableCommand(() => ItemCount--, () => ItemCount > MinItems);
        SyncItemSlots(); // start with a single slot

        _selectedSlotOption = SlotOptions.FirstOrDefault(s => s.Slot == ItemSlot.Ring) ?? SlotOptions[0];
        RefreshBaseOptions();
        RefreshModGroups();
        InitPresets();
    }

    private void RefreshBaseOptions()
    {
        BaseOptions.Clear();
        var slot = _selectedSlotOption?.Slot ?? ItemSlot.Ring;

        _selectedBaseOption = null;
        _selectedJewelType  = null;
        _selectedTabletType = null;
        _selectedInfluence  = null;
        BaseVisibility       = Visibility.Collapsed;
        JewelTypeVisibility  = Visibility.Collapsed;
        TabletTypeVisibility = Visibility.Collapsed;
        InfluenceVisibility  = Visibility.Collapsed;

        if (_profile.ShowBaseFor(slot))
        {
            foreach (var kv in _profile.ArmourBaseDisplayNames.Where(kv => kv.Key != ArmourBase.None))
                BaseOptions.Add(new BaseOption(kv.Key, kv.Value));
            _selectedBaseOption = BaseOptions.FirstOrDefault();
            BaseVisibility = Visibility.Visible;
        }
        else if (_profile.ShowJewelTypeFor(slot))
        {
            JewelTypeOptions.Clear();
            foreach (var kv in _profile.JewelTypesFor(slot))
                JewelTypeOptions.Add(new JewelTypeOption(kv.Key, kv.Value));
            _selectedJewelType = JewelTypeOptions.FirstOrDefault();
            JewelTypeVisibility = Visibility.Visible;
        }
        else if (_profile.ShowTabletTypeFor(slot))
        {
            _selectedTabletType = TabletTypeOptions.FirstOrDefault();
            TabletTypeVisibility = Visibility.Visible;
        }
        RefreshClusterBases();

        // Influence is an independent dimension — can combine with armour bases
        if (_profile.ShowInfluenceFor(slot) && InfluenceOptions.Count > 0)
        {
            _selectedInfluence = InfluenceOptions.FirstOrDefault(o => o.Influence == Influence.None)
                                 ?? InfluenceOptions.FirstOrDefault();
            InfluenceVisibility = Visibility.Visible;
        }

        Notify(nameof(SelectedBaseOption));
        Notify(nameof(SelectedJewelType));
        Notify(nameof(SelectedTabletType));
        Notify(nameof(SelectedInfluence));
    }

    // Cluster jewels have a second dimension: the base enchant type. The list
    // depends on the chosen size and comes from ClusterJewels.lua.
    private void RefreshClusterBases()
    {
        var slot = _selectedSlotOption?.Slot ?? ItemSlot.Ring;
        ClusterBaseOptions.Clear();
        _selectedClusterBase = null;
        ClusterBaseVisibility = Visibility.Collapsed;

        if (slot == ItemSlot.ClusterJewel && _selectedJewelType is not null)
        {
            foreach (var b in _profile.ClusterBasesFor(_selectedJewelType.Type))
                ClusterBaseOptions.Add(b);
            _selectedClusterBase = ClusterBaseOptions.FirstOrDefault();
            if (ClusterBaseOptions.Count > 0) ClusterBaseVisibility = Visibility.Visible;
        }
        Notify(nameof(SelectedClusterBase));
    }

    private void RefreshModGroups()
    {
        var influence = _selectedInfluence?.Influence ?? Influence.None;
        var selection = new SlotSelection(
            _selectedSlotOption?.Slot ?? ItemSlot.Ring,
            _selectedBaseOption?.Base ?? ArmourBase.None,
            _selectedJewelType?.Type ?? JewelType.None,
            _selectedTabletType?.Type ?? TabletType.None,
            influence,
            _selectedClusterBase?.Tag);
        var groups = _db.GetGroups(_profile, selection);

        // Make the influence effect visible: groups that exist only thanks to
        // the influence are starred and float to the top
        if (influence != Influence.None)
        {
            var plainIds = _db.GetGroups(_profile, selection with { Influence = Influence.None })
                .Select(g => g.GroupId)
                .ToHashSet();
            foreach (var g in groups)
                g.Highlight = !plainIds.Contains(g.GroupId);
            groups = groups.OrderByDescending(g => g.Highlight).ThenBy(g => g.DisplayName).ToList();
        }

        _allGroups = groups;
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
        try
        {
            var item = ItemParser.TryParse(clipboardText);

            // Update hash even on parse fail — empty hash prevents false matches in AutoCrafter
            LastItemHash = item is { Mods.Count: > 0 }
                ? string.Join("|", item.Mods.Select(m => m.Text))
                : "";

            // Confirm a pending apply: currency was used and the item came back
            // readable, so exactly one orb is accounted for.
            if (_applyPending && LastItemHash != "")
            {
                _applyPending = false;
                SessionSpent++;
                TotalSpent++;
            }

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

            LastCraftAction = IsAltAugMode
                ? AltAugPolicy.Decide(item, conditions, result, _db)
                : (result.AllMatched ? CraftAction.Stop : CraftAction.UseAlt);

            MatchedLines.Clear();
            StatusVisibility = Visibility.Visible;
            IsStop = result.AllMatched;

            if (result.AllMatched)
            {
                StatusText  = "⛔  STOP";
                StatusBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B));
                foreach (var h in result.Hits)
                    MatchedLines.Add($"✓  {h.Target.DisplayName}  T{h.Tier} [{h.Value}]");
                MatchedLines.Add($"◆  потрачено: {SessionSpent}");
            }
            else if (LastCraftAction == CraftAction.Abort)
            {
                StatusText  = $"⚠  {AltAugPolicy.AbortReason(item) ?? "Alt+Aug: нельзя продолжить"}";
                StatusBrush = new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22));
                foreach (var m in result.Misses)
                    MatchedLines.Add($"✗  {m.DisplayName}");
            }
            else
            {
                StatusText  = !IsAltAugMode ? "✓  GO — keep rolling"
                             : LastCraftAction == CraftAction.UseAug ? "✓  GO — Aug"
                             : "✓  GO — Alt";
                StatusBrush = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
                foreach (var h in result.Hits)
                    MatchedLines.Add($"✓  {h.Target.DisplayName}  T{h.Tier} [{h.Value}]");
                foreach (var m in result.Misses)
                    MatchedLines.Add($"✗  {m.DisplayName}");
            }
        }
        finally
        {
            _evalSeq++; // signal AutoCrafter: clipboard processed, IsStop/hash are fresh
        }

        // Budget stop comes after the status update: a roll that hits the target
        // on the very last orb still shows STOP rather than a limit warning.
        if (IsRunning && !IsStop && StopAfter > 0 && SessionSpent >= StopAfter)
            StopRequested?.Invoke($"Лимит {StopAfter} — потрачено, стоп");
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
        ApplySelection(s.Slot, s.ArmourBase, s.JewelType, s.TabletType, s.Influence, s.ClusterBase);

        IsAutoMode   = s.IsAutoMode;
        IsAltAugMode = string.Equals(s.CraftMode, "AltAug", StringComparison.OrdinalIgnoreCase);
        StopAfter    = s.StopAfter;
        TotalSpent   = s.TotalSpent;
    }

    // Restore the item context (slot → base/jewel/tablet/influence). Order
    // matters: the slot refresh rebuilds the dependent option lists.
    private void ApplySelection(string? slot, string? armourBase, string? jewelType,
                                string? tabletType, string? influence, string? clusterBase)
    {
        if (Enum.TryParse<ItemSlot>(slot, out var s))
        {
            var opt = SlotOptions.FirstOrDefault(o => o.Slot == s);
            if (opt != null) SelectedSlotOption = opt; // triggers base/jewel refresh
        }
        if (Enum.TryParse<ArmourBase>(armourBase, out var ab))
        {
            var opt = BaseOptions.FirstOrDefault(o => o.Base == ab);
            if (opt != null) SelectedBaseOption = opt;
        }
        if (Enum.TryParse<JewelType>(jewelType, out var jt))
        {
            var opt = JewelTypeOptions.FirstOrDefault(o => o.Type == jt);
            if (opt != null) SelectedJewelType = opt;
        }
        if (Enum.TryParse<TabletType>(tabletType, out var tt))
        {
            var opt = TabletTypeOptions.FirstOrDefault(o => o.Type == tt);
            if (opt != null) SelectedTabletType = opt;
        }
        if (Enum.TryParse<Influence>(influence, out var inf))
        {
            var opt = InfluenceOptions.FirstOrDefault(o => o.Influence == inf);
            if (opt != null) SelectedInfluence = opt;
        }
        if (clusterBase != null)
        {
            var opt = ClusterBaseOptions.FirstOrDefault(o => o.Tag == clusterBase);
            if (opt != null) SelectedClusterBase = opt;
        }
    }

    public void FillSettings(Services.AppSettings s)
    {
        s.Slot       = SelectedSlotOption?.Slot.ToString();
        s.ArmourBase = SelectedBaseOption?.Base.ToString();
        s.JewelType  = SelectedJewelType?.Type.ToString();
        s.TabletType = SelectedTabletType?.Type.ToString();
        s.Influence  = SelectedInfluence?.Influence.ToString();
        s.ClusterBase = SelectedClusterBase?.Tag;
        s.IsAutoMode  = IsAutoMode;
        s.CraftMode   = IsAltAugMode ? "AltAug" : "ChaosAlt";
        s.StopAfter   = StopAfter;
        s.TotalSpent  = TotalSpent;
    }

    // ── Craft presets ─────────────────────────────────────────────────
    // The whole file is kept in memory (both games) so saving one game's preset
    // never drops the other's. The visible list is filtered to the active game:
    // a PoE1 group id means nothing in PoE2.
    private List<Services.CraftPreset> _allPresets = [];

    public ObservableCollection<Services.CraftPreset> Presets { get; } = [];

    private Services.CraftPreset? _selectedPreset;
    public Services.CraftPreset? SelectedPreset
    {
        get => _selectedPreset;
        set { Set(ref _selectedPreset, value); if (value != null) NewPresetName = value.Name; }
    }

    private string _newPresetName = "";
    public string NewPresetName
    {
        get => _newPresetName;
        set => Set(ref _newPresetName, value);
    }

    public RelayCommand SavePresetCommand   { get; private set; } = null!;
    public RelayCommand LoadPresetCommand   { get; private set; } = null!;
    public RelayCommand DeletePresetCommand { get; private set; } = null!;

    private void InitPresets()
    {
        SavePresetCommand   = new RelayCommand(SavePreset);
        LoadPresetCommand   = new RelayCommand(LoadPreset);
        DeletePresetCommand = new RelayCommand(DeletePreset);

        _allPresets = Services.PresetStore.Load();
        RefreshPresetList();
    }

    private void RefreshPresetList()
    {
        Presets.Clear();
        foreach (var p in _allPresets.Where(p => p.Game == GameKey)
                                     .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
            Presets.Add(p);
    }

    private void SavePreset()
    {
        var name = NewPresetName.Trim();
        if (name.Length == 0) name = SelectedPreset?.Name ?? "";
        if (name.Length == 0) { ShowNotice("Введи имя пресета"); return; }
        if (TargetMods.Count == 0) { ShowNotice("Нечего сохранять — список целей пуст"); return; }

        var preset = new Services.CraftPreset
        {
            Name        = name,
            Game        = GameKey,
            Slot        = SelectedSlotOption?.Slot.ToString(),
            ArmourBase  = SelectedBaseOption?.Base.ToString(),
            JewelType   = SelectedJewelType?.Type.ToString(),
            TabletType  = SelectedTabletType?.Type.ToString(),
            Influence   = SelectedInfluence?.Influence.ToString(),
            ClusterBase = SelectedClusterBase?.Tag,
            CraftMode   = IsAltAugMode ? "AltAug" : "ChaosAlt",
            StopAfter   = StopAfter,
            Targets     = TargetMods.Select(t => new Services.PresetTarget
            {
                GroupId = t.Group.GroupId,
                Tier    = t.TargetTier.Tier,
                Exact   = t.IsExact,
            }).ToList(),
        };

        // Same name in the same game overwrites — "Save" doubling as "update"
        // is what people expect from a preset box.
        _allPresets.RemoveAll(p => p.Game == GameKey &&
                                   string.Equals(p.Name, name, StringComparison.CurrentCultureIgnoreCase));
        _allPresets.Add(preset);
        Services.PresetStore.Save(_allPresets);
        RefreshPresetList();
        SelectedPreset = Presets.FirstOrDefault(p => ReferenceEquals(p, preset));
        ShowNotice($"Пресет «{name}» сохранён");
    }

    private void LoadPreset()
    {
        var preset = SelectedPreset;
        if (preset is null) { ShowNotice("Выбери пресет"); return; }

        ApplySelection(preset.Slot, preset.ArmourBase, preset.JewelType,
                       preset.TabletType, preset.Influence, preset.ClusterBase);
        IsAltAugMode = string.Equals(preset.CraftMode, "AltAug", StringComparison.OrdinalIgnoreCase);
        StopAfter    = preset.StopAfter;

        // Rebuild the targets against the freshly refreshed group list. A group
        // that can't spawn under this selection (data update, wrong base) is
        // reported instead of being silently dropped — a half-loaded target list
        // is exactly the kind of surprise STOP we avoid elsewhere.
        TargetMods.Clear();
        int missing = 0;
        foreach (var t in preset.Targets)
        {
            var group = _allGroups.FirstOrDefault(g => g.GroupId == t.GroupId);
            if (group is null) { missing++; continue; }
            var tier = group.Tiers.FirstOrDefault(x => x.Tier == t.Tier) ?? group.Tiers[0];
            TargetMods.Add(new TargetModViewModel(group, tier) { IsExact = t.Exact });
        }
        TargetListVisibility = TargetMods.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        ShowNotice(missing == 0
            ? $"Пресет «{preset.Name}»: целей {TargetMods.Count}"
            : $"Пресет «{preset.Name}»: целей {TargetMods.Count}, недоступно {missing}");
    }

    private void DeletePreset()
    {
        var preset = SelectedPreset;
        if (preset is null) return;
        _allPresets.Remove(preset);
        Services.PresetStore.Save(_allPresets);
        RefreshPresetList();
        SelectedPreset = null;
        NewPresetName  = "";
        ShowNotice($"Пресет «{preset.Name}» удалён");
    }
}
