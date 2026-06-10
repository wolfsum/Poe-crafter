using Poe2Crafter.Core.Models;

namespace Poe2Crafter.ViewModels;

public record SlotOption(ItemSlot Slot, string Name)
{
    public override string ToString() => Name;
}

public record BaseOption(ArmourBase Base, string Name)
{
    public override string ToString() => Name;
}

public record JewelTypeOption(JewelType Type, string Name)
{
    public override string ToString() => Name;
}

public record TabletTypeOption(TabletType Type, string Name)
{
    public override string ToString() => Name;
}
