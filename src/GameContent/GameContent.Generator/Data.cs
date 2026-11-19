// Authoring tables for the equipment, vehicle and era packs. The generator turns these into the packs' JSON; the JSON is the product.
static partial class Data
{
    // era key -> era id, order, tier, label, from, to (years; null for fictional)
    public static readonly (string Key, string Id, string Name, int Order, int Tier, string Label, int? From, int? To)[] AgesEras =
    [
        ("stone", "ages.stone", "Stone Age", 10, 0, "stone-age", -3000000, -4500),
        ("copper", "ages.copper", "Copper Age", 15, 1, "copper-age", -4500, -3300),
        ("bronze", "ages.bronze", "Bronze Age", 20, 2, "bronze-age", -3300, -1200),
        ("iron", "ages.iron", "Iron Age", 30, 3, "iron-age", -1200, -500),
        ("classical", "ages.classical", "Classical age", 40, 4, "classical", -500, 500),
        ("medieval", "ages.medieval", "Medieval age", 50, 5, "medieval", 500, 1400),
        ("renaissance", "ages.renaissance", "Renaissance", 55, 6, "renaissance", 1400, 1600),
        ("gunpowder", "ages.gunpowder", "Gunpowder age", 60, 7, "gunpowder", 1600, 1750),
        ("industrial", "ages.industrial", "Industrial age", 70, 8, "industrial", 1750, 1900),
        ("modern", "ages.modern", "Modern age", 80, 9, "modern", 1900, 1945),
        ("atomic", "ages.atomic", "Atomic age", 90, 10, "atomic", 1945, 1991),
        ("information", "ages.information", "Information age", 100, 11, "information", 1991, 2025),
    ];

    public static readonly (string Key, string Id, string Name, int Order, int Tier, string Label)[] HorizonEras =
    [
        ("space", "horizons.space-age", "Early space age", 110, 12, "space-age"),
        ("near", "horizons.near-future", "Near future", 120, 13, "near-future"),
        ("far", "horizons.far-future", "Far future", 130, 14, "far-future"),
    ];

    // Stat vocabulary of the gear pack: id slug, name, dimension, unit, kind, min, max, enum values, classes.
    public const string Weapons = "weapon,vehicle-land,vehicle-sea,vehicle-air,vehicle-space,structure-unit";
    public const string Vehicles = "vehicle-land,vehicle-sea,vehicle-air,vehicle-space";
    public const string AllClasses = "weapon,armor,tool,vehicle-land,vehicle-sea,vehicle-air,vehicle-space,mount,structure-unit";

    public static readonly (string Slug, string Name, string Dimension, string Unit, string Kind, string? Min, string? Max, string Values, string Classes)[] Stats =
    [
        ("damage", "Damage", "none", "1", "integer", "0", "10000", "", Weapons + ",mount,ammunition"),
        ("damage-type", "Damage type", "none", "1", "enum", null, null, "slash,pierce,blunt,ballistic,blast,energy,fire", Weapons + ",mount,ammunition"),
        ("range", "Effective range", "length", "m", "number", "0", "1000000", "", Weapons + ",mount"),
        ("rate-of-fire", "Rate of fire", "rate", "/min", "number", "0", "100000", "", Weapons + ",mount"),
        ("accuracy", "Accuracy", "ratio", "%", "number", "1", "100", "", Weapons + ",mount"),
        ("penetration", "Armor penetration", "none", "1", "integer", "0", "10000", "", Weapons + ",mount,ammunition"),
        ("splash-radius", "Splash radius", "length", "m", "number", "0", "10000", "", Weapons + ",ammunition"),
        ("armor-rating", "Armor rating", "none", "1", "integer", "0", "10000", "", "armor,vehicle-land,vehicle-sea,vehicle-air,vehicle-space,mount,structure-unit"),
        ("structure", "Structure points", "none", "1", "integer", "1", "1000000000", "", "vehicle-land,vehicle-sea,vehicle-air,vehicle-space,mount,structure-unit"),
        ("weight", "Weight", "mass", "kg", "number", "0", "1000000000", "", AllClasses + ",ammunition"),
        ("speed", "Top speed", "speed", "km/h", "number", "0", "100000", "", Vehicles + ",mount"),
        ("operating-range", "Operating range", "length", "km", "number", "0", "100000000", "", Vehicles),
        ("ceiling", "Service ceiling", "length", "m", "number", "0", "100000000", "", "vehicle-air,vehicle-space"),
        ("reliability", "Reliability", "ratio", "%", "number", "0", "100", "", "weapon,tool,vehicle-land,vehicle-sea,vehicle-air,vehicle-space,structure-unit"),
        ("efficiency", "Work efficiency", "ratio", "%", "number", "0", "1000", "", "tool"),
    ];

    public static readonly string[] Skills = ["driving|Driving", "sailing|Sailing", "piloting|Piloting", "gunnery|Gunnery", "riding|Riding", "starflight|Starflight"];

    // Material line: slug|name|class|unit|origin|price|era|ingredients|station|outAmount|kind|minutes
    // era: "-" timeless, or an ages/horizons key. ingredients "-" for natural. price "auto" for crafted.
    public static readonly string[] Materials =
    [
        // natural
        "flint-nodule|Raw flint|raw|kg|natural|0.2|-|-|-|1|-|0",
        "animal-bone|Animal bone|raw|kg|natural|0.3|-|-|-|1|-|0",
        "sinew|Animal sinew|raw|kg|natural|2|-|-|-|1|-|0",
        "rawhide|Rawhide|raw|kg|natural|1|-|-|-|1|-|0",
        "ash-wood|Ash wood|raw|kg|natural|0.6|-|-|-|1|-|0",
        "yew-wood|Yew wood|raw|kg|natural|1.5|-|-|-|1|-|0",
        "titanium-ore|Titanium ore|raw|kg|natural|3|-|-|-|1|-|0",
        "rare-earth|Rare earth oxide|raw|kg|natural|20|-|-|-|1|-|0",
        "deuterium|Deuterium|fluid|L|natural|40|-|-|-|1|-|0",
        "mithril-ore|Mithril ore|raw|kg|natural|30|-|-|-|1|-|0",
        "dragon-scale|Dragon scale|raw|kg|natural|200|-|-|-|1|-|0",
        "moon-crystal|Moon crystal|raw|kg|natural|60|-|-|-|1|-|0",
        // stone age
        "flint-blank|Knapped flint blank|intermediate|{each}|crafted|auto|stone|matter.flint-nodule:1;core.labor:0.1|core.workbench:1|2|craft|20",
        "cured-hide|Cured hide|intermediate|kg|crafted|auto|stone|matter.rawhide:1.2;core.salt:0.1|core.workbench:1|1|craft|90",
        "sling-stone|Sling stone|consumable|{each}|crafted|auto|stone|core.stone:0.5|core.workbench:1|10|craft|15",
        "arrow-flint|Flint-tipped arrow|consumable|{each}|crafted|auto|stone|matter.ash-wood:0.1;matter.flint-blank:2;matter.sinew:0.02|core.workbench:1|10|craft|30",
        // bronze and iron
        "arrow-bronze|Bronze-tipped arrow|consumable|{each}|crafted|auto|bronze|matter.ash-wood:0.1;core.bronze-ingot:0.05;matter.sinew:0.02|core.workbench:1|10|craft|30",
        "scale-plates|Bronze armor scales|intermediate|kg|crafted|auto|bronze|core.bronze-ingot:1.1|core.furnace:1|1|craft|60",
        "mail-rings|Riveted mail rings|intermediate|kg|crafted|auto|iron|core.iron-ingot:1.2|core.furnace:1|1|craft|120",
        "arrow-iron|Iron-headed arrow|consumable|{each}|crafted|auto|iron|matter.ash-wood:0.1;core.iron-ingot:0.05;matter.sinew:0.02|core.workbench:1|10|craft|30",
        // medieval
        "armor-plate|Hardened steel plate|intermediate|kg|crafted|auto|medieval|core.steel-ingot:1.2|core.furnace:2|1|craft|150",
        "bolt|Crossbow bolt|consumable|{each}|crafted|auto|medieval|matter.ash-wood:0.08;core.steel-ingot:0.03|core.workbench:2|10|craft|30",
        "arrow-bodkin|Bodkin arrow|consumable|{each}|crafted|auto|medieval|matter.yew-wood:0.08;core.steel-ingot:0.03;matter.sinew:0.02|core.workbench:2|10|craft|30",
        // gunpowder
        "black-powder|Black powder|intermediate|kg|crafted|auto|renaissance|mining.nitrate:0.75;core.charcoal:0.15;mining.sulphur:0.1|core.workbench:2|1|craft|60",
        "musket-ball|Lead musket ball|consumable|{each}|crafted|auto|renaissance|mining.lead-ingot:0.5|core.workbench:2|20|craft|40",
        "musket-round|Paper cartridge|consumable|{each}|crafted|auto|renaissance|matter.musket-ball:10;matter.black-powder:0.2;core.fiber:0.1|core.workbench:2|10|craft|30",
        "round-shot|Iron round shot|consumable|{each}|crafted|auto|renaissance|core.iron-ingot:6|core.furnace:2|4|craft|90",
        // industrial
        "smokeless-powder|Smokeless powder|intermediate|kg|crafted|auto|industrial|core.fiber:1;mining.nitrate:0.5;core.water:1|core.workbench:2|1|craft|90",
        "brass-casing|Brass cartridge case|intermediate|{each}|crafted|auto|industrial|mining.brass-ingot:0.5|core.workbench:2|20|craft|40",
        "rifle-round|Metallic rifle cartridge|consumable|{each}|crafted|auto|industrial|matter.brass-casing:20;matter.smokeless-powder:0.1;mining.lead-ingot:0.4|core.workbench:2|20|craft|40",
        "field-shell|Field gun shell|consumable|{each}|crafted|auto|industrial|core.steel-ingot:8;matter.smokeless-powder:1|core.furnace:2|10|craft|120",
        "steam-engine|Steam engine|intermediate|{each}|crafted|auto|industrial|core.steel-ingot:8;mining.brass-ingot:3;core.labor:6|core.workbench:2|1|assemble|300",
        "piston-engine|Internal combustion engine|intermediate|{each}|crafted|auto|industrial|core.steel-ingot:10;mining.aluminium-ingot:2;mining.brass-ingot:1;core.labor:8|core.workbench:2|1|assemble|420",
        // modern
        "titanium-ingot|Titanium ingot|intermediate|kg|crafted|auto|atomic|matter.titanium-ore:3;core.electricity:20|core.furnace:3|1|craft|180",
        "carbon-fibre|Carbon fibre|intermediate|kg|crafted|auto|atomic|core.crude-oil:3;core.electricity:10|core.furnace:2|1|craft|120",
        "armor-ceramic|Armor ceramic plate|intermediate|{each}|crafted|auto|atomic|core.clay:3;core.electricity:15|core.furnace:3|1|craft|150",
        "aramid|Aramid fabric|intermediate|kg|crafted|auto|atomic|core.crude-oil:2;core.electricity:8|core.furnace:2|1|craft|100",
        "jet-turbine|Jet turbine|intermediate|{each}|crafted|auto|atomic|matter.titanium-ingot:6;mining.nickel-ingot:8;core.labor:20|core.furnace:3|1|assemble|600",
        // near future
        "superconductor|Superconductor coil|intermediate|{each}|crafted|auto|near|matter.rare-earth:2;core.copper-ingot:3;core.electricity:30|core.furnace:3|1|craft|180",
        "graphene|Graphene sheet|intermediate|kg|crafted|auto|near|core.coal:5;core.electricity:40|core.furnace:3|1|craft|150",
        "reactive-tile|Reactive armor tile|intermediate|{each}|crafted|auto|near|matter.armor-ceramic:2;core.steel-ingot:2;matter.smokeless-powder:0.2|core.furnace:3|1|craft|90",
        "energy-mag|Energy magazine|consumable|{each}|crafted|auto|near|electronics.cell:2;core.electricity:5|core.workbench:2|1|craft|20",
        // far future
        "plasma-cell|Plasma cell|consumable|{each}|crafted|auto|far|matter.deuterium:0.5;matter.superconductor:1;core.electricity:50|core.furnace:3|5|craft|60",
        "fusion-core|Fusion core|intermediate|{each}|crafted|auto|far|matter.deuterium:20;matter.superconductor:6;matter.graphene:4;core.labor:30|core.furnace:3|1|assemble|1200",
        "hull-alloy|Composite hull alloy|intermediate|kg|crafted|auto|far|matter.titanium-ingot:4;matter.graphene:2;core.electricity:60|core.furnace:3|4|craft|240",
        "focus-lens|Hardlight focus lens|intermediate|{each}|crafted|auto|far|electronics.lens:4;matter.rare-earth:1;core.electricity:10|core.workbench:2|1|craft|90",
        // fantasy (timeless materials, used by the fantasy pack)
        "mithril-ingot|Mithril ingot|intermediate|kg|crafted|auto|-|matter.mithril-ore:3;core.coal:2|rpg.forge:3|1|craft|240",
        "rune-stone|Rune-etched stone|intermediate|{each}|crafted|auto|-|matter.moon-crystal:1;core.stone:2|rpg.enchanting-table:1|1|craft|90",
        "dragon-leather|Dragon-scale leather|intermediate|kg|crafted|auto|-|matter.dragon-scale:3;matter.cured-hide:1|rpg.tannery:1|1|craft|240",
    ];

    // Item line: pack|era|slug|name|class|replaces|stats|durability|ingredients|station|minutes|extra
    //   stats: dmg dt rng rof arm wt spd trv ceil rel eff     extra: slots=hand:1 cargo=mass:500 crew=gunnery:1x2 fuel=res:cap:burn ammo=res:cap upkeep=res:perday natural
    public static readonly string[] Items =
    [
        // ---------------- stone age
        "ancient|stone|flint-knife|Flint knife|weapon|-|dmg=5,dt=slash,rng=1,rof=60,wt=0.2,rel=80|80|matter.flint-blank:1;core.fiber:0.1|core.workbench:1|15|slots=hand:1",
        "ancient|stone|hand-axe|Flint hand axe|tool|-|wt=1.2,eff=40,rel=75|120|matter.flint-blank:2;core.wood:0.5;core.fiber:0.2|core.workbench:1|25|slots=hand:1",
        "ancient|stone|wooden-club|Wooden club|weapon|-|dmg=6,dt=blunt,rng=1,rof=40,wt=1.5,rel=95|150|core.wood:1.5|core.workbench:1|20|slots=hand:1",
        "ancient|stone|stone-axe|Hafted stone axe|weapon|ancient.flint-knife|dmg=9,dt=slash,rng=1,rof=35,wt=1.6,rel=85|180|matter.flint-blank:2;core.wood:1;matter.sinew:0.1|core.workbench:1|40|slots=hand:1",
        "ancient|stone|throwing-spear|Throwing spear|weapon|ancient.wooden-club|dmg=10,dt=pierce,rng=20,rof=6,wt=1,rel=80|100|matter.ash-wood:1;matter.flint-blank:1;matter.sinew:0.1|core.workbench:1|35|slots=hand:1",
        "ancient|stone|sling|Hide sling|weapon|-|dmg=7,dt=blunt,rng=60,rof=12,wt=0.1,rel=90|120|matter.sinew:0.2;matter.rawhide:0.3|core.workbench:1|20|slots=hand:1;ammo=matter.sling-stone:30",
        "ancient|stone|hunting-bow|Self bow|weapon|ancient.sling|dmg=14,dt=pierce,rng=50,rof=10,wt=1,rel=85|200|matter.ash-wood:1.2;matter.sinew:0.3|core.workbench:1|90|slots=hand:2;ammo=matter.arrow-flint:20",
        "ancient|stone|hide-wrap|Hide wrap|armor|-|arm=3,wt=2|200|matter.rawhide:3;matter.sinew:0.2|core.workbench:1|60|slots=body:1",
        "ancient|stone|bone-awl|Bone awl|tool|-|wt=0.1,eff=30,rel=80|90|matter.animal-bone:0.5|core.workbench:1|10|slots=hand:1",
        "ancient|stone|dugout-canoe|Dugout canoe|vehicle-sea|-|spd=6,trv=40,wt=90,rel=85|400|core.wood:40;core.labor:6|core.workbench:1|480|cargo=mass:200;crew=sailing:1x2",
        "ancient|stone|log-raft|Log raft|vehicle-sea|-|spd=3,trv=15,wt=250,rel=70|250|core.wood:30;core.fiber:5;core.labor:3|core.workbench:1|240|cargo=mass:300;crew=sailing:1x1",
        "ancient|stone|travois|Travois|vehicle-land|-|spd=4,trv=20,wt=15,rel=90|300|core.wood:6;matter.rawhide:2;core.fiber:1|core.workbench:1|90|cargo=mass:60;crew=driving:1x1",
        "ancient|stone|pack-ox|Tamed pack ox|mount|-|spd=5,arm=2,wt=600|0|-|-|0|cargo=mass:150;fuel=core.grain:20:0.6;upkeep=core.grain:6;natural",
        // ---------------- bronze age
        "ancient|bronze|bronze-dagger|Bronze dagger|weapon|ancient.copper-dagger|dmg=10,dt=pierce,rng=1,rof=55,wt=0.4,rel=92|300|core.bronze-ingot:0.4;core.wood:0.1|core.furnace:1|40|slots=hand:1",
        "ancient|bronze|bronze-axe|Bronze battle axe|weapon|ancient.copper-axe|dmg=16,dt=slash,rng=1,rof=35,wt=1.4,rel=92|400|core.bronze-ingot:1;core.wood:0.8|core.furnace:1|60|slots=hand:1",
        "ancient|bronze|bronze-spear|Bronze-tipped spear|weapon|ancient.throwing-spear|dmg=18,dt=pierce,rng=3,rof=30,wt=1.8,rel=92|350|core.bronze-ingot:0.5;matter.ash-wood:1.2|core.furnace:1|50|slots=hand:1",
        "ancient|bronze|short-sword|Bronze short sword|weapon|ancient.bronze-dagger|dmg=20,dt=slash,rng=1,rof=45,wt=0.9,rel=92|450|core.bronze-ingot:1.2;matter.cured-hide:0.2|core.furnace:1|90|slots=hand:1",
        "ancient|bronze|composite-bow|Composite bow|weapon|ancient.hunting-bow|dmg=20,dt=pierce,rng=80,rof=10,wt=1.2,rel=88|300|matter.ash-wood:1;matter.animal-bone:0.5;matter.sinew:0.6|core.workbench:1|240|slots=hand:2;ammo=matter.arrow-bronze:20",
        "ancient|bronze|wicker-shield|Hide-covered wicker shield|armor|-|arm=6,wt=3|250|core.wood:1;matter.cured-hide:1.5|core.workbench:1|90|slots=hand:1",
        "ancient|bronze|scale-armor|Bronze scale armor|armor|ancient.hide-wrap|arm=14,wt=8|600|matter.scale-plates:6;matter.cured-hide:2|core.furnace:1|240|slots=body:1",
        "ancient|bronze|bronze-helmet|Bronze helmet|armor|-|arm=8,wt=1.2|500|core.bronze-ingot:1.5|core.furnace:1|90|slots=head:1",
        "ancient|bronze|bronze-sickle|Bronze sickle|tool|-|wt=0.5,eff=60,rel=92|700|core.bronze-ingot:0.6;core.wood:0.2|core.furnace:1|50|slots=hand:1",
        "ancient|bronze|bronze-chisel|Bronze chisel|tool|ancient.bone-awl|wt=0.3,eff=70,rel=94|800|core.bronze-ingot:0.4|core.furnace:1|45|slots=hand:1",
        "ancient|bronze|chariot|Two-wheeled chariot|vehicle-land|ancient.travois|spd=30,trv=60,wt=120,rel=80|600|core.wood:25;core.bronze-ingot:3;matter.cured-hide:4;core.rope:2|core.workbench:2|720|cargo=mass:150;crew=driving:1x2",
        "ancient|bronze|galley|Oared galley|vehicle-sea|ancient.dugout-canoe|spd=12,trv=200,wt=6000,rel=80|1500|naval.oak:120;core.bronze-ingot:8;core.rope:20;naval.canvas:10|naval.slipway:1|2880|cargo=mass:3000;crew=sailing:2x20",
        "ancient|bronze|riding-horse|Domesticated horse|mount|ancient.pack-ox|spd=40,arm=2,wt=450|0|-|-|0|cargo=mass:120;fuel=core.grain:25:0.8;upkeep=core.grain:8;natural",
        // ---------------- iron age
        "ancient|iron|iron-blade|Iron blade|weapon|ancient.short-sword|dmg=26,dt=slash,rng=1,rof=45,wt=1.1,rel=94|700|core.iron-ingot:1.5;core.wood:0.2|core.furnace:2|120|slots=hand:1",
        "ancient|iron|iron-axe|Iron battle axe|weapon|ancient.bronze-axe|dmg=24,dt=slash,rng=1,rof=35,wt=1.5,rel=94|800|core.iron-ingot:1.3;core.wood:0.8|core.furnace:2|90|slots=hand:1",
        "ancient|iron|iron-spear|Iron-headed spear|weapon|ancient.bronze-spear|dmg=24,dt=pierce,rng=3,rof=30,wt=2,rel=94|700|core.iron-ingot:0.7;matter.ash-wood:1.2|core.furnace:2|70|slots=hand:1",
        "ancient|iron|steel-bow|Reinforced war bow|weapon|ancient.composite-bow|dmg=26,dt=pierce,rng=90,rof=10,wt=1.3,rel=90|350|matter.ash-wood:1.2;matter.animal-bone:0.5;matter.sinew:0.7|core.workbench:2|260|slots=hand:2;ammo=matter.arrow-iron:20",
        "ancient|iron|mail-shirt|Mail shirt|armor|ancient.scale-armor|arm=24,wt=11|1200|matter.mail-rings:9;matter.cured-hide:1|core.furnace:2|600|slots=body:1",
        "ancient|iron|iron-helmet|Iron helmet|armor|ancient.bronze-helmet|arm=14,wt=1.5|900|core.iron-ingot:2|core.furnace:2|100|slots=head:1",
        "ancient|iron|bossed-shield|Iron-bossed shield|armor|ancient.wicker-shield|arm=12,wt=4|700|core.wood:2;core.iron-ingot:0.6;matter.cured-hide:1.5|core.workbench:2|120|slots=hand:1",
        "ancient|iron|iron-ploughshare|Iron ploughshare|tool|ancient.bronze-sickle|wt=6,eff=110,rel=96|1500|core.iron-ingot:4;core.wood:1|core.furnace:2|180|slots=hand:2",
        "ancient|iron|iron-pick|Iron mattock|tool|ancient.bronze-chisel|wt=3,eff=100,rel=96|1400|core.iron-ingot:2.5;core.wood:1|core.furnace:2|150|slots=hand:2",
        "ancient|iron|war-chariot|Iron-tyred war chariot|vehicle-land|ancient.chariot|spd=38,trv=80,wt=160,arm=6,rel=82|800|core.wood:30;core.iron-ingot:6;matter.cured-hide:5;core.rope:3|core.workbench:2|900|cargo=mass:200;crew=driving:1x2",
        "ancient|iron|merchant-ship|Merchant sailing ship|vehicle-sea|ancient.galley|spd=14,trv=1500,wt=20000,rel=82|2500|naval.oak:300;core.iron-ingot:20;naval.sail:1;core.rope:40;naval.pine-tar:20|naval.slipway:1|7200|cargo=mass:12000;crew=sailing:2x12",
        "ancient|iron|trained-warhorse|Trained warhorse|mount|ancient.riding-horse|spd=45,arm=6,rng=2,rof=20,dmg=8,dt=blunt,wt=500|0|-|-|0|cargo=mass:130;fuel=core.grain:30:1;upkeep=core.grain:10;natural",
        // ---------------- classical
        "ancient|classical|stabbing-sword|Short stabbing sword|weapon|ancient.iron-blade|dmg=30,dt=pierce,rng=1,rof=55,wt=0.9,rel=95|900|core.iron-ingot:1.3;core.wood:0.2|core.furnace:2|110|slots=hand:1",
        "ancient|classical|heavy-javelin|Weighted heavy javelin|weapon|ancient.iron-spear|dmg=32,dt=pierce,rng=30,rof=6,wt=2.2,rel=92|400|core.iron-ingot:0.6;matter.ash-wood:1|core.furnace:2|60|slots=hand:1",
        "ancient|classical|segmented-cuirass|Segmented plate cuirass|armor|ancient.mail-shirt|arm=34,wt=9|1800|core.iron-ingot:9;matter.cured-hide:2;core.rope:2|core.furnace:2|720|slots=body:1",
        "ancient|classical|tower-shield|Curved tower shield|armor|ancient.bossed-shield|arm=20,wt=7|1100|core.plank:4;matter.cured-hide:2;core.iron-ingot:0.8|core.workbench:2|180|slots=hand:1",
        "ancient|classical|torsion-catapult|Torsion catapult|structure-unit|-|dmg=80,dt=blunt,rng=300,rof=1,wt=900,rel=70|900|core.plank:30;core.iron-ingot:12;matter.sinew:6;core.rope:10|core.workbench:2|2400|crew=gunnery:1x4;ammo=matter.sling-stone:20",
        "ancient|classical|bolt-thrower|Bolt thrower|structure-unit|-|dmg=45,dt=pierce,rng=400,rof=4,wt=350,rel=75|700|core.plank:14;core.iron-ingot:6;matter.sinew:3;core.rope:6|core.workbench:2|1500|crew=gunnery:1x2;ammo=matter.arrow-iron:40",
        "ancient|classical|road-cart|Road cart|vehicle-land|ancient.war-chariot|spd=7,trv=40,wt=300,rel=90|1500|core.plank:24;core.iron-ingot:5;core.rope:6|core.workbench:2|600|cargo=mass:800;crew=driving:1x1",
        "ancient|classical|trireme|Trireme|vehicle-sea|ancient.merchant-ship|spd=18,trv=300,wt=40000,rng=5,rof=2,dmg=40,dt=blunt,rel=80|3000|naval.oak:400;core.bronze-ingot:40;naval.sail:1;core.rope:60;naval.pine-tar:30|naval.slipway:2|10800|cargo=mass:6000;crew=sailing:3x170",
        "ancient|classical|grain-carrier|Grain carrier|vehicle-sea|ancient.merchant-ship|spd=10,trv=2500,wt=120000,rel=85|4000|naval.oak:900;core.iron-ingot:60;naval.sail:3;core.rope:100;naval.pine-tar:60|naval.slipway:2|14400|cargo=mass:80000;crew=sailing:3x20",
        "ancient|classical|war-elephant|Trained war elephant|mount|-|spd=25,arm=15,rng=3,rof=15,dmg=60,dt=blunt,wt=5000|0|-|-|0|cargo=mass:600;fuel=core.grain:100:4;upkeep=core.grain:60;natural",
        "ancient|classical|dressed-stone-pick|Hardened iron pick|tool|ancient.iron-pick|wt=3.2,eff=130,rel=97|2000|core.iron-ingot:3;core.wood:1|core.furnace:2|160|slots=hand:2",
        // ---------------- medieval
        "medieval|medieval|twohand-sword|Two-handed longsword|weapon|ancient.stabbing-sword|dmg=38,dt=slash,rng=1,rof=40,wt=1.6,rel=96|1100|core.steel-ingot:2;core.wood:0.2;matter.cured-hide:0.2|core.furnace:2|240|slots=hand:2",
        "medieval|medieval|arming-sword|Arming sword|weapon|ancient.stabbing-sword|dmg=33,dt=slash,rng=1,rof=50,wt=1.1,rel=96|1000|core.steel-ingot:1.5;core.wood:0.2|core.furnace:2|180|slots=hand:1",
        "medieval|medieval|war-hammer|Steel war hammer|weapon|ancient.iron-axe|dmg=36,dt=blunt,rng=1,rof=35,wt=1.8,rel=96|1100|core.steel-ingot:1.6;core.wood:0.6|core.furnace:2|150|slots=hand:1",
        "medieval|medieval|longbow|English-style longbow|weapon|ancient.steel-bow|dmg=34,dt=pierce,rng=250,rof=12,wt=1.2,rel=92|450|matter.yew-wood:2;matter.sinew:0.2|core.workbench:2|480|slots=hand:2;ammo=matter.arrow-bodkin:24",
        "medieval|medieval|crossbow|Steel prod crossbow|weapon|ancient.steel-bow|dmg=44,dt=pierce,rng=300,rof=3,wt=3.5,rel=94|700|matter.ash-wood:1.5;core.steel-ingot:1.2;matter.sinew:0.5;core.rope:1|core.workbench:2|360|slots=hand:2;ammo=matter.bolt:20",
        "medieval|medieval|halberd|Halberd|weapon|ancient.heavy-javelin|dmg=42,dt=slash,rng=3,rof=25,wt=2.6,rel=95|1000|core.steel-ingot:1.4;matter.ash-wood:1.5|core.furnace:2|200|slots=hand:2",
        "medieval|medieval|plate-harness|Plate harness|armor|ancient.segmented-cuirass|arm=55,wt=22|3500|matter.armor-plate:20;matter.cured-hide:3;core.rope:2|core.furnace:2|1800|slots=body:1",
        "medieval|medieval|great-helm|Great helm|armor|ancient.iron-helmet|arm=22,wt=2.4|1500|matter.armor-plate:2.5|core.furnace:2|240|slots=head:1",
        "medieval|medieval|heater-shield|Heater shield|armor|ancient.tower-shield|arm=26,wt=4|1400|core.plank:2;matter.armor-plate:1;matter.cured-hide:1|core.workbench:2|200|slots=hand:1",
        "medieval|medieval|trebuchet|Counterweight trebuchet|structure-unit|ancient.torsion-catapult|dmg=160,dt=blunt,rng=300,rof=0.5,wt=12000,rel=80|2000|core.plank:80;core.iron-ingot:20;core.rope:30;core.stone:200|core.workbench:2|7200|crew=gunnery:2x10;ammo=matter.sling-stone:40",
        "medieval|medieval|destrier|Armoured destrier|mount|ancient.trained-warhorse|spd=40,arm=14,rng=2,rof=20,dmg=14,dt=blunt,wt=600|0|-|-|0|cargo=mass:180;fuel=core.grain:35:1.2;upkeep=core.grain:12;natural",
        "medieval|medieval|cog|Cog|vehicle-sea|ancient.grain-carrier|spd=11,trv=2000,wt=150000,rel=86|5000|naval.oak:1000;core.iron-ingot:70;naval.sail:2;naval.rigging:1;core.rope:80;naval.pine-tar:80|naval.slipway:2|18000|cargo=mass:100000;crew=sailing:3x20",
        "medieval|medieval|four-horse-wagon|Four-horse wagon|vehicle-land|ancient.road-cart|spd=9,trv=60,wt=800,rel=90|2500|core.plank:50;core.iron-ingot:12;core.rope:10;naval.pine-tar:6|core.workbench:2|1200|cargo=mass:2500;crew=driving:2x2",
        "medieval|medieval|castle-ballista|Great ballista|structure-unit|ancient.bolt-thrower|dmg=70,dt=pierce,rng=500,rof=3,wt=800,rel=80|1300|core.plank:24;core.steel-ingot:8;core.rope:12;matter.sinew:5|core.workbench:2|2400|crew=gunnery:2x3;ammo=matter.bolt:30",
        // ---------------- gunpowder
        "medieval|gunpowder|matchlock-musket|Matchlock musket|weapon|medieval.arquebus|dmg=70,dt=ballistic,rng=100,rof=1.5,wt=5,rel=70|1100|core.steel-ingot:2.5;matter.ash-wood:2;core.fiber:0.5|core.furnace:2|360|slots=hand:2;ammo=matter.musket-round:20",
        "medieval|gunpowder|flintlock-musket|Flintlock musket|weapon|medieval.matchlock-musket|dmg=75,dt=ballistic,rng=120,rof=3,wt=4.5,rel=82|1500|core.steel-ingot:2.6;matter.ash-wood:2;core.bronze-ingot:0.2|core.furnace:2|420|slots=hand:2;ammo=matter.musket-round:24",
        "medieval|gunpowder|flintlock-pistol|Flintlock pistol|weapon|medieval.wheel-lock-pistol|dmg=55,dt=ballistic,rng=25,rof=2,wt=1.2,rel=78|900|core.steel-ingot:0.9;matter.ash-wood:0.4|core.furnace:2|240|slots=hand:1;ammo=matter.musket-round:6",
        "medieval|gunpowder|cavalry-saber|Cavalry saber|weapon|medieval.arming-sword|dmg=40,dt=slash,rng=1,rof=50,wt=1.0,rel=97|1400|core.steel-ingot:1.6;core.wood:0.2|core.furnace:2|200|slots=hand:1",
        "medieval|gunpowder|breastplate|Proof breastplate|armor|medieval.cuirass-tassets|arm=45,wt=10|2500|matter.armor-plate:9;matter.cured-hide:1|core.furnace:2|900|slots=body:1",
        "medieval|gunpowder|bronze-cannon|Bronze field cannon|structure-unit|medieval.culverin|dmg=250,dt=blast,rng=800,rof=1,wt=1500,rel=75|3000|core.bronze-ingot:150;core.plank:20;core.iron-ingot:10|core.furnace:3|7200|crew=gunnery:2x5;ammo=matter.round-shot:30",
        "medieval|gunpowder|siege-mortar|Siege mortar|structure-unit|medieval.trebuchet|dmg=400,dt=blast,rng=1200,rof=0.5,wt=3500,rel=72|3000|core.iron-ingot:300;core.plank:30;core.rope:10|core.furnace:3|9000|crew=gunnery:2x6;ammo=matter.round-shot:20",
        "medieval|gunpowder|caravel|Caravel|vehicle-sea|-|spd=15,trv=4000,wt=60000,rel=85|5000|naval.oak:600;core.iron-ingot:40;naval.sail:3;naval.rigging:2;naval.anchor:1;naval.pine-tar:60|naval.slipway:2|14400|cargo=mass:50000;crew=sailing:3x25",
        "medieval|gunpowder|sailing-frigate|Sailing frigate|vehicle-sea|medieval.carrack|spd=22,trv=6000,wt=900000,arm=40,rng=1200,dmg=300,dt=blast,rof=2,rel=85|9000|naval.oak:3000;core.iron-ingot:150;naval.sail:6;naval.rigging:3;naval.anchor:2;naval.deck-gun:24;naval.pine-tar:200|naval.slipway:3|43200|cargo=mass:80000;crew=sailing:4x250;ammo=matter.round-shot:600",
        "medieval|gunpowder|ship-of-the-line|Ship of the line|vehicle-sea|medieval.sailing-frigate|spd=20,trv=7000,wt=3000000,arm=70,rng=1500,dmg=900,dt=blast,rof=2,rel=85|15000|naval.oak:8000;core.iron-ingot:400;naval.sail:10;naval.rigging:6;naval.anchor:3;naval.deck-gun:74;naval.pine-tar:500|naval.slipway:3|86400|cargo=mass:150000;crew=sailing:5x700;ammo=matter.round-shot:1500",
        "medieval|gunpowder|stagecoach|Stagecoach|vehicle-land|medieval.four-horse-wagon|spd=18,trv=150,wt=900,rel=88|3500|core.plank:40;core.iron-ingot:10;core.rope:8;matter.cured-hide:12|core.workbench:2|1500|cargo=mass:900;crew=driving:2x2",
        "medieval|gunpowder|powder-horn|Powder horn|tool|-|wt=0.3,eff=60,rel=90|300|matter.animal-bone:0.4;matter.cured-hide:0.2|core.workbench:1|40|slots=belt:1",
        "medieval|gunpowder|bayonet|Socket bayonet|weapon|-|dmg=38,dt=pierce,rng=1,rof=40,wt=0.5,rel=97|1400|core.steel-ingot:0.5|core.furnace:2|60|slots=muzzle:1",
        // ---------------- industrial
        "modern|industrial|rifled-musket|Rifled musket|weapon|medieval.flintlock-musket|dmg=90,dt=ballistic,rng=400,rof=3,wt=4.3,rel=88|2500|core.steel-ingot:2.8;matter.ash-wood:2;mining.brass-ingot:0.2|core.furnace:2|480|slots=hand:2;ammo=matter.musket-round:24",
        "modern|industrial|revolver|Six-shot revolver|weapon|medieval.flintlock-pistol|dmg=60,dt=ballistic,rng=40,rof=24,wt=1.1,rel=90|2000|core.steel-ingot:1;mining.brass-ingot:0.3;matter.ash-wood:0.2|core.furnace:2|300|slots=hand:1;ammo=matter.rifle-round:6",
        "modern|industrial|repeating-rifle|Lever-action repeating rifle|weapon|modern.rifled-musket|dmg=85,dt=ballistic,rng=300,rof=12,wt=3.9,rel=90|2500|core.steel-ingot:2.4;matter.ash-wood:1.5;mining.brass-ingot:0.4|core.furnace:2|480|slots=hand:2;ammo=matter.rifle-round:15",
        "modern|industrial|crank-gun|Rotary crank gun|weapon|-|dmg=80,dt=ballistic,rng=500,rof=300,wt=180,rel=75|3000|core.steel-ingot:30;mining.brass-ingot:6;core.plank:6;core.labor:20|core.workbench:2|2400|crew=gunnery:1x3;ammo=matter.rifle-round:500",
        "modern|industrial|breech-field-gun|Rifled breech-loading field gun|structure-unit|medieval.bronze-cannon|dmg=420,dt=blast,rng=4000,rof=4,wt=1800,rel=82|4500|core.steel-ingot:220;core.plank:20;core.labor:30|core.furnace:3|8000|crew=gunnery:3x6;ammo=matter.field-shell:60",
        "modern|industrial|steam-locomotive|Steam locomotive|vehicle-land|medieval.stagecoach|spd=100,trv=900,wt=40000,rel=85|9000|core.steel-ingot:100;mining.brass-ingot:10;matter.steam-engine:1;core.plank:30;core.labor:200|core.furnace:3|14400|cargo=mass:200000;fuel=core.coal:6000:800;crew=driving:3x2",
        "modern|industrial|steamship|Steamship|vehicle-sea|medieval.ship-of-the-line|spd=24,trv=8000,wt=4000000,rel=88|20000|core.steel-ingot:600;matter.steam-engine:2;naval.anchor:2;core.plank:200;core.labor:500|naval.slipway:3|72000|cargo=mass:2500000;fuel=core.coal:900000:6000;crew=sailing:4x120",
        "modern|industrial|ironclad|Ironclad warship|vehicle-sea|modern.steamship|spd=18,trv=3500,wt=9000000,arm=400,rng=4000,dmg=1200,dt=blast,rof=2,rel=85|25000|core.steel-ingot:1200;matter.steam-engine:2;naval.anchor:2;core.labor:900|naval.slipway:3|86400|cargo=mass:100000;fuel=core.coal:400000:5000;crew=sailing:5x300;ammo=matter.field-shell:200",
        "modern|industrial|steam-tractor|Steam traction engine|vehicle-land|-|spd=12,trv=120,wt=9000,rel=82|8000|core.steel-ingot:30;matter.steam-engine:1;core.plank:10;core.labor:60|core.workbench:2|7200|cargo=mass:12000;fuel=core.coal:800:60;crew=driving:2x2",
        "modern|industrial|observation-balloon|Observation balloon|vehicle-air|-|spd=15,ceil=1200,trv=50,wt=600,rel=70|2000|core.cloth:120;core.rope:40;core.plank:6;core.labor:60|core.workbench:2|4800|cargo=mass:250;crew=piloting:1x2",
        "modern|industrial|rigid-airship|Rigid airship|vehicle-air|modern.observation-balloon|spd=110,ceil=3000,trv=6000,wt=60000,rel=75|8000|mining.aluminium-ingot:300;core.cloth:2000;matter.piston-engine:4;core.labor:600|core.workbench:2|43200|cargo=mass:20000;fuel=mining.diesel:30000:400;crew=piloting:3x30",
        "modern|industrial|safety-bicycle|Safety bicycle|vehicle-land|-|spd=22,trv=100,wt=14,rel=95|3000|core.steel-ingot:2;core.rope:2;core.cloth:0.5;core.labor:6|core.workbench:2|240|cargo=mass:30;crew=driving:1x1",
        "modern|industrial|steam-hammer|Steam hammer|tool|-|wt=12000,eff=400,rel=85|9000|core.steel-ingot:50;matter.steam-engine:1;core.labor:80|core.furnace:3|7200|crew=driving:1x2",
        // ---------------- modern
        "modern|modern|bolt-rifle|Bolt-action rifle|weapon|modern.repeating-rifle|dmg=110,dt=ballistic,rng=800,rof=15,wt=4,rel=96|4000|core.steel-ingot:2.4;matter.ash-wood:1.5|core.furnace:3|360|slots=hand:2;ammo=matter.rifle-round:5",
        "modern|modern|self-loading-rifle|Self-loading rifle|weapon|modern.bolt-rifle|dmg=105,dt=ballistic,rng=700,rof=60,wt=4.4,rel=95|4500|core.steel-ingot:2.6;mining.aluminium-ingot:0.5|core.furnace:3|420|slots=hand:2;ammo=matter.rifle-round:20",
        "modern|atomic|assault-rifle|Assault rifle|weapon|modern.self-loading-rifle|dmg=95,dt=ballistic,rng=500,rof=600,wt=3.6,rel=95|5000|core.steel-ingot:2.2;mining.aluminium-ingot:0.6;matter.carbon-fibre:0.4|core.furnace:3|420|slots=hand:2;ammo=matter.rifle-round:30",
        "modern|modern|light-machine-gun|Light machine gun|weapon|modern.crank-gun|dmg=100,dt=ballistic,rng=900,rof=700,wt=9,rel=93|6000|core.steel-ingot:6;mining.aluminium-ingot:1|core.furnace:3|600|slots=hand:2;ammo=matter.rifle-round:200;crew=gunnery:1x2",
        "modern|atomic|combat-helmet|Combat helmet|armor|medieval.great-helm|arm=40,wt=1.3|3000|matter.aramid:1.2;matter.carbon-fibre:0.5|core.furnace:2|120|slots=head:1",
        "modern|atomic|ballistic-vest|Ballistic vest|armor|medieval.breastplate|arm=70,wt=5|4000|matter.aramid:4;matter.armor-ceramic:2|core.furnace:2|300|slots=body:1",
        "modern|information|plate-carrier|Ceramic plate carrier|armor|modern.ballistic-vest|arm=110,wt=9|6000|matter.aramid:3;matter.armor-ceramic:4;core.cloth:1|core.furnace:2|360|slots=body:1",
        "modern|modern|medium-tank|Medium tank|vehicle-land|-|dmg=600,dt=ballistic,rng=3000,rof=8,arm=700,spd=55,trv=450,wt=45000,rel=90|50000|core.steel-ingot:900;matter.piston-engine:2;mining.nickel-ingot:60;core.labor:800|military.vehicle-works:3|28800|cargo=mass:800;fuel=mining.diesel:900:90;crew=driving:3x4;ammo=matter.field-shell:60",
        "modern|atomic|armored-carrier|Armored personnel carrier|vehicle-land|-|dmg=60,dt=ballistic,rng=1200,rof=500,arm=300,spd=90,trv=600,wt=20000,rel=92|30000|core.steel-ingot:420;matter.piston-engine:1;mining.aluminium-ingot:40;core.labor:500|military.vehicle-works:2|21600|cargo=population:10;fuel=mining.diesel:500:50;crew=driving:2x2",
        "modern|modern|cargo-truck|Cargo truck|vehicle-land|medieval.stagecoach|spd=90,trv=800,wt=9000,rel=94|20000|core.steel-ingot:120;matter.piston-engine:1;core.cloth:10;core.labor:200|military.vehicle-works:2|14400|cargo=mass:10000;fuel=mining.diesel:400:30;crew=driving:2x1",
        "modern|modern|utility-car|Utility car|vehicle-land|modern.safety-bicycle|spd=130,trv=700,wt=1400,rel=95|15000|core.steel-ingot:30;matter.piston-engine:1;core.glass:12;core.labor:120|military.vehicle-works:2|7200|cargo=population:5;fuel=mining.diesel:60:7;crew=driving:1x1",
        "modern|modern|destroyer|Destroyer|vehicle-sea|modern.ironclad|spd=60,trv=9000,wt=7000000,arm=300,rng=20000,dmg=1500,dt=blast,rof=20,rel=92|80000|core.steel-ingot:3000;matter.piston-engine:4;mining.nickel-ingot:200;naval.anchor:2;core.labor:2500|naval.slipway:3|172800|cargo=mass:100000;fuel=mining.diesel:900000:6000;crew=sailing:5x250;ammo=matter.field-shell:600",
        "modern|modern|submarine|Submarine|vehicle-sea|-|spd=40,trv=12000,wt=2500000,arm=150,rng=10000,dmg=1800,dt=blast,rof=6,rel=90|90000|core.steel-ingot:2000;matter.piston-engine:4;mining.nickel-ingot:160;core.labor:3000|naval.slipway:3|216000|cargo=mass:30000;fuel=mining.diesel:400000:2500;crew=sailing:5x60;ammo=matter.field-shell:24",
        "modern|modern|biplane|Biplane|vehicle-air|-|spd=190,ceil=5000,trv=500,wt=900,rel=80|9000|core.plank:20;core.cloth:80;mining.aluminium-ingot:10;matter.piston-engine:1;core.labor:120|core.workbench:2|10800|cargo=mass:120;fuel=mining.diesel:120:30;crew=piloting:2x1",
        "modern|atomic|jet-fighter|Jet fighter|vehicle-air|modern.biplane|spd=2000,ceil=15000,trv=2500,rng=3000,dmg=900,dt=ballistic,rof=6000,arm=60,wt=14000,rel=88|120000|mining.aluminium-ingot:2500;matter.titanium-ingot:500;matter.jet-turbine:1;electronics.control-board:6;core.labor:2500|military.vehicle-works:3|259200|cargo=mass:2000;fuel=mining.diesel:6000:2500;crew=piloting:5x1;ammo=matter.rifle-round:500",
        "modern|atomic|transport-aircraft|Transport aircraft|vehicle-air|modern.rigid-airship|spd=800,ceil=10000,trv=7000,wt=70000,rel=92|200000|mining.aluminium-ingot:6000;matter.titanium-ingot:400;matter.jet-turbine:4;electronics.control-board:10;core.labor:5000|military.vehicle-works:3|432000|cargo=mass:40000;fuel=mining.diesel:60000:15000;crew=piloting:4x3",
        "modern|atomic|helicopter|Utility helicopter|vehicle-air|-|spd=250,ceil=5000,trv=700,wt=4500,rel=86|90000|mining.aluminium-ingot:800;matter.carbon-fibre:150;matter.jet-turbine:1;electronics.control-board:4;core.labor:1500|military.vehicle-works:3|144000|cargo=population:12;fuel=mining.diesel:1500:300;crew=piloting:4x2",
        "modern|modern|chainsaw|Petrol chainsaw|tool|-|wt=6,eff=500,rel=88|2500|core.steel-ingot:1.5;mining.aluminium-ingot:1;mining.diesel:0.5;core.labor:6|core.workbench:2|120|slots=hand:2;fuel=mining.diesel:0.5:0.3",
    ];
}
