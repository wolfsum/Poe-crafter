using Poe2Crafter.Services;

namespace Poe2Crafter.ViewModels;

// One item slot in the auto-craft queue. The crafter rolls each slot in turn
// until its targets are hit, then advances to the next. Pos is the on-screen
// point captured by clicking the item in-game.
public class ItemSlotViewModel : ViewModelBase
{
    public ItemSlotViewModel(int number) => _number = number;

    private int _number;
    public int Number
    {
        get => _number;
        set { Set(ref _number, value); Notify(nameof(Label)); }
    }

    public string Label => $"Item {Number}";

    private bool _isSet;
    public bool IsSet
    {
        get => _isSet;
        set => Set(ref _isSet, value);
    }

    public NativeMethods.POINT Pos { get; set; }
}
