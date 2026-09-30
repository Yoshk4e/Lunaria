namespace Lunaria.Game.Resources;

public sealed record CharacterRow(
    uint CharacterId,
    uint Identity, // Identity_Type: 1 Hero, 2 Guardian, 3 Outlaw, 4 Victim, 5 Saviour, 6 Witness
    uint Element, // Element_Type: 1 Ignis, 2 Glacies, 3 Fulmen, 4 Gravitas, 5 Radiatio, 6 Ferrugo, 7 Alba
    uint Rarity, // 4 or 5 stars
    uint[] SkillGroups
);
