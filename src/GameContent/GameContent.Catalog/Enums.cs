namespace GameContent.Catalog;

/// <summary>What a resource is. The JSON name is the lower-case, hyphenated member name.</summary>
public enum ResourceClass { Raw, Intermediate, Product, Consumable, Currency, Energy, Fluid, Gas, Information, Labor, Population, Tool, Equipment, Unique }

/// <summary>Whether a resource exists in the world without a recipe (natural) or only as something made (crafted).</summary>
public enum ResourceOrigin { Natural, Crafted }

public enum GradeScheme { Quality, Rarity }

public enum CurrencyKind { Iso4217, Fictional }

/// <summary>What a recipe does. Generative kinds may bring a resource into the world without consuming it.</summary>
public enum RecipeKind
{
    Extract, Harvest, Grow, Transform, Smelt, Refine, Craft, Assemble, Disassemble, Cook, Brew, Combine,
    Upgrade, Repair, Construct, Research, Trade, Upkeep, Consume, Decay, Loot,
}

public enum QualityMode { InheritInput, Skill }

public enum TimeUnit { S, Min, H, D }

/// <summary>What a pack's world is. A timeless pack has no eras. A historical pack orders its eras on real years and stops at the present; a fictional one orders them freely.</summary>
public enum WorldKind { Timeless, Historical, Fictional }

/// <summary>The closed vocabulary of period names, in their order in time. An era names one of these as its label so eras of different packs stay comparable.</summary>
public enum PeriodLabel { StoneAge, CopperAge, BronzeAge, IronAge, Classical, Medieval, Renaissance, Gunpowder, Industrial, Modern, Atomic, Information, SpaceAge, NearFuture, FarFuture }

public enum StatKind { Number, Integer, Enum }

/// <summary>What an item is for. The class decides which stats, links and resource classes make sense for it.</summary>
public enum ItemClass { Weapon, Armor, Tool, VehicleLand, VehicleSea, VehicleAir, VehicleSpace, Mount, StructureUnit, Ammunition }

/// <summary>How well drilled a formation is. Combat rules turn each level into accuracy and damage modifiers.</summary>
public enum TrainingLevel { Raw, Regular, Veteran, Elite }
