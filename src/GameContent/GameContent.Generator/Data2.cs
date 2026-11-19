static partial class Data
{
    // More materials: second ways to make shared parts (so no part is a single point of failure) are the lines marked "alt".
    public static readonly string[] AltMaterials =
    [
        "cured-hide|Cured hide|intermediate|kg|crafted|auto|stone|matter.rawhide:1.5;core.wood:1|core.workbench:1|1|craft|240",
        "superconductor|Superconductor coil|intermediate|{each}|crafted|auto|near|matter.rare-earth:3;core.copper-ingot:4;core.electricity:36|core.furnace:3|1|craft|240",
        "titanium-ingot|Titanium ingot|intermediate|kg|crafted|auto|atomic|matter.titanium-ore:3.5;core.coal:2|core.furnace:3|1|craft|240",
        "graphene|Graphene sheet|intermediate|kg|crafted|auto|near|core.crude-oil:6;core.electricity:30|core.furnace:3|1|craft|180",
        "hull-alloy|Composite hull alloy|intermediate|kg|crafted|auto|far|matter.carbon-fibre:5;matter.graphene:2;core.electricity:50|core.furnace:3|4|craft|300",
        "armor-ceramic|Armor ceramic plate|intermediate|{each}|crafted|auto|atomic|core.clay:4;core.sand:2;core.coal:2|core.furnace:3|1|craft|200",
        "aramid|Aramid fabric|intermediate|kg|crafted|auto|atomic|core.crude-oil:3;core.coal:1;core.electricity:5|core.furnace:2|1|craft|140",
        "carbon-fibre|Carbon fibre|intermediate|kg|crafted|auto|atomic|core.coal:4;core.electricity:12|core.furnace:2|1|craft|150",
        "piston-engine|Internal combustion engine|intermediate|{each}|crafted|auto|industrial|core.steel-ingot:12;mining.brass-ingot:2;core.labor:10|core.workbench:2|1|assemble|480",
        "jet-turbine|Jet turbine|intermediate|{each}|crafted|auto|atomic|matter.titanium-ingot:5;mining.nickel-ingot:10;matter.carbon-fibre:5;core.labor:24|core.furnace:3|1|assemble|700",
        "armor-plate|Hardened steel plate|intermediate|kg|crafted|auto|medieval|core.iron-ingot:1.4;core.charcoal:0.5|core.furnace:2|1|craft|240",
        "mail-rings|Riveted mail rings|intermediate|kg|crafted|auto|iron|core.iron-ingot:1.1;core.coal:0.1|core.furnace:2|1|craft|150",
        "black-powder|Black powder|intermediate|kg|crafted|auto|renaissance|mining.nitrate:0.7;core.charcoal:0.2;mining.sulphur:0.1|core.workbench:2|1|craft|75",
        "fusion-core|Fusion core|intermediate|{each}|crafted|auto|far|matter.deuterium:25;matter.superconductor:5;matter.graphene:5;core.labor:36|core.furnace:3|1|assemble|1500",
    ];

    public static readonly string[] FutureItems =
    [
        // ---------------- near future
        "future|near|railgun-carbine|Railgun carbine|weapon|modern.assault-rifle|dmg=140,dt=ballistic,rng=1500,rof=120,wt=4.5,rel=92|8000|electronics.power-pack:2;matter.superconductor:2;core.steel-ingot:2;electronics.control-board:1|electronics.assembly-line:1|480|slots=hand:2;ammo=matter.energy-mag:10",
        "future|near|laser-dazzler|Laser dazzler|weapon|-|dmg=20,dt=energy,rng=800,rof=120,wt=2,rel=94|6000|electronics.lens:2;electronics.power-pack:1;matter.superconductor:1;electronics.control-board:1|electronics.assembly-line:1|300|slots=hand:1;ammo=matter.energy-mag:20",
        "future|near|smart-helmet|Smart helmet|armor|modern.combat-helmet|arm=60,wt=1.6|7000|matter.aramid:1;electronics.display:1;electronics.control-board:1|electronics.assembly-line:1|180|slots=head:1",
        "future|near|reactive-vest|Reactive armor vest|armor|modern.plate-carrier|arm=160,wt=8|9000|matter.reactive-tile:6;matter.aramid:2|core.furnace:3|300|slots=body:1",
        "future|near|exo-frame|Powered exo-frame|armor|future.reactive-vest|arm=220,wt=35|40000|matter.graphene:6;matter.carbon-fibre:10;electronics.power-pack:2;electronics.control-board:2;matter.superconductor:4|electronics.assembly-line:1|1200|slots=body:1;fuel=core.electricity:20:2",
        "future|near|power-loader|Power loader|tool|-|wt=900,eff=800,rel=94|30000|matter.titanium-ingot:30;electronics.power-pack:3;electronics.control-board:2;matter.superconductor:4|electronics.assembly-line:1|900|fuel=core.electricity:30:3",
        "future|near|hover-truck|Hover truck|vehicle-land|modern.cargo-truck|spd=160,trv=900,wt=7000,rel=95|60000|matter.carbon-fibre:300;electronics.power-pack:12;matter.superconductor:6;electronics.control-board:4|electronics.assembly-line:1|7200|cargo=mass:15000;fuel=core.electricity:800:100;crew=driving:2x1",
        "future|near|electric-battle-tank|Electric battle tank|vehicle-land|modern.medium-tank|dmg=900,dt=ballistic,rng=4500,rof=10,arm=1200,spd=70,trv=500,wt=50000,rel=94|120000|matter.titanium-ingot:200;matter.reactive-tile:40;electronics.power-pack:30;matter.superconductor:10;electronics.control-board:6|military.vehicle-works:3|36000|cargo=mass:600;fuel=core.electricity:1500:150;crew=driving:3x3;ammo=matter.field-shell:60",
        "future|near|cargo-drone|Autonomous cargo drone|vehicle-air|modern.helicopter|spd=120,ceil=3000,trv=300,wt=250,rel=93|20000|matter.carbon-fibre:60;electronics.power-pack:4;electronics.control-board:2;electronics.sensor:2|electronics.assembly-line:1|2400|cargo=mass:800;fuel=core.electricity:200:40",
        "future|near|tiltrotor|Tilt-rotor transport|vehicle-air|modern.transport-aircraft|spd=520,ceil=7000,trv=1800,wt=12000,rel=93|150000|matter.titanium-ingot:150;matter.carbon-fibre:400;electronics.power-pack:20;electronics.control-board:6;matter.superconductor:6|military.vehicle-works:3|72000|cargo=population:24;fuel=core.electricity:1800:400;crew=piloting:4x2",
        "future|near|orbital-lifter|Orbital lifter|vehicle-space|-|spd=28000,ceil=400000,trv=800000,wt=250000,rel=90|900000|matter.titanium-ingot:2000;matter.jet-turbine:6;electronics.control-board:12;matter.carbon-fibre:1500|space.shipyard:2|216000|cargo=mass:30000;fuel=matter.deuterium:30000:6000;crew=starflight:4x2",
        "future|near|autonomous-freighter|Autonomous freighter|vehicle-sea|-|spd=35,trv=15000,wt=40000000,rel=94|600000|matter.titanium-ingot:3000;electronics.power-pack:100;electronics.control-board:20;electronics.sensor:10|naval.slipway:3|259200|cargo=mass:30000000;fuel=core.electricity:80000:6000",
        // ---------------- far future
        "future|far|plasma-rifle|Plasma rifle|weapon|future.railgun-carbine|dmg=400,dt=energy,rng=900,rof=90,wt=5,rel=90|10000|electronics.power-pack:3;matter.focus-lens:1;matter.superconductor:3;electronics.control-board:2|electronics.assembly-line:1|600|slots=hand:2;ammo=matter.plasma-cell:20",
        "future|far|hardlight-blade|Hardlight blade|weapon|-|dmg=350,dt=energy,rng=2,rof=80,wt=0.6,rel=95|9000|matter.focus-lens:2;electronics.power-pack:2;matter.superconductor:2|electronics.assembly-line:1|480|slots=hand:1;fuel=core.electricity:10:2",
        "future|far|pulse-sidearm|Pulse sidearm|weapon|modern.revolver|dmg=200,dt=energy,rng=60,rof=240,wt=0.8,rel=94|8000|matter.focus-lens:1;electronics.power-pack:1;matter.superconductor:1|electronics.assembly-line:1|300|slots=hand:1;ammo=matter.energy-mag:30",
        "future|far|nanoweave-suit|Nanoweave suit|armor|future.exo-frame|arm=350,wt=6|50000|matter.hull-alloy:6;matter.graphene:8;matter.aramid:5;electronics.control-board:3|electronics.assembly-line:1|900|slots=body:1",
        "future|far|deflector-harness|Deflector harness|armor|future.nanoweave-suit|arm=500,wt=7|90000|matter.hull-alloy:4;matter.focus-lens:2;electronics.power-pack:4;matter.superconductor:4|electronics.assembly-line:1|1200|slots=body:1;fuel=core.electricity:40:4",
        "future|far|starfighter|Starfighter|vehicle-space|-|dmg=1500,dt=energy,rng=20000,rof=600,arm=400,spd=90000,ceil=100000000,trv=5000000,wt=15000,rel=94|400000|matter.hull-alloy:200;matter.fusion-core:1;electronics.control-board:8;matter.focus-lens:6|space.shipyard:2|180000|cargo=mass:500;fuel=matter.deuterium:2000:80;crew=starflight:6x1;ammo=matter.plasma-cell:400",
        "future|far|bulk-freighter|Bulk freighter|vehicle-space|-|spd=60000,ceil=100000000,trv=20000000,arm=200,wt=2000000,rel=96|3000000|matter.hull-alloy:3000;matter.fusion-core:2;electronics.control-board:20|space.shipyard:3|864000|cargo=mass:50000000;fuel=matter.deuterium:200000:600;crew=starflight:5x20",
        "future|far|dropship|Atmospheric dropship|vehicle-space|future.orbital-lifter|spd=45000,ceil=100000000,trv=100000,arm=300,rng=5000,dmg=800,dt=energy,rof=300,wt=60000,rel=94|500000|matter.hull-alloy:600;matter.fusion-core:1;electronics.control-board:10;matter.focus-lens:4|space.shipyard:2|259200|cargo=population:40;fuel=matter.deuterium:8000:600;crew=starflight:6x2;ammo=matter.plasma-cell:300",
        "future|far|walker-mech|Combat walker|vehicle-land|future.electric-battle-tank|dmg=1200,dt=energy,rng=6000,rof=120,arm=900,spd=60,trv=1000,wt=40000,rel=93|700000|matter.hull-alloy:150;matter.fusion-core:1;electronics.control-board:10;matter.superconductor:12|military.vehicle-works:3|120000|cargo=mass:400;fuel=core.electricity:4000:300;crew=driving:5x1;ammo=matter.plasma-cell:200",
        "future|far|colony-ship|Colony ship|vehicle-space|-|spd=30000,ceil=100000000,trv=90000000,arm=500,wt=90000000,rel=97|60000000|matter.hull-alloy:20000;matter.fusion-core:6;electronics.control-board:60|space.shipyard:3|2592000|cargo=population:5000;fuel=matter.deuterium:900000:200;crew=starflight:7x120",
        "future|far|jump-module|Jump drive module|tool|-|wt=8000,eff=1000,rel=92|900000|matter.fusion-core:1;matter.hull-alloy:100;matter.focus-lens:8;matter.superconductor:20|space.shipyard:3|86400|fuel=matter.deuterium:2000:200",
        "future|far|defence-turret|Orbital defence turret|structure-unit|-|dmg=900,dt=energy,rng=20000,rof=600,arm=800,wt=30000,rel=96|600000|matter.hull-alloy:40;electronics.power-pack:20;matter.superconductor:8;electronics.control-board:6|space.shipyard:2|86400|crew=gunnery:5x1;ammo=matter.plasma-cell:2000",
        "future|far|grav-bike|Grav bike|vehicle-land|-|spd=350,trv=1500,wt=300,rel=92|40000|matter.carbon-fibre:80;matter.superconductor:6;electronics.power-pack:4;electronics.control-board:2|electronics.assembly-line:1|2400|cargo=mass:100;fuel=core.electricity:100:25;crew=driving:2x1",
    ];

    public static readonly string[] FantasyItems =
    [
        "fantasy|medieval|mithril-longsword|Mithril longsword|weapon|medieval.twohand-sword|dmg=70,dt=slash,rng=1,rof=45,wt=1.2,rel=99|5000|matter.mithril-ingot:2;matter.rune-stone:1;core.labor:6|rpg.forge:4|600|slots=hand:2",
        "fantasy|medieval|spellblade|Spellblade|weapon|fantasy.mithril-longsword|dmg=85,dt=energy,rng=1,rof=45,wt=1.1,rel=99|6000|matter.mithril-ingot:1.5;matter.rune-stone:2;core.labor:8|rpg.forge:4|720|slots=hand:1",
        "fantasy|medieval|elder-bow|Elder bow|weapon|medieval.longbow|dmg=60,dt=pierce,rng=400,rof=14,wt=1,rel=98|3000|matter.yew-wood:3;matter.rune-stone:1;matter.sinew:0.4|core.workbench:2|600|slots=hand:2;ammo=matter.arrow-bodkin:30",
        "fantasy|medieval|runic-staff|Runic staff|weapon|-|dmg=90,dt=energy,rng=150,rof=20,wt=2,rel=97|3500|matter.ash-wood:2;matter.rune-stone:3;matter.moon-crystal:1|rpg.enchanting-table:2|720|slots=hand:2",
        "fantasy|medieval|mithril-mail|Mithril mail|armor|medieval.plate-harness|arm=120,wt=7|8000|matter.mithril-ingot:8;matter.cured-hide:1;core.labor:10|rpg.forge:4|1200|slots=body:1",
        "fantasy|medieval|dragon-scale-armor|Dragon-scale armor|armor|fantasy.mithril-mail|arm=200,wt=12|20000|matter.dragon-leather:8;matter.mithril-ingot:2;core.labor:12|rpg.tannery:1|1800|slots=body:1",
        "fantasy|medieval|rune-shield|Rune shield|armor|medieval.heater-shield|arm=90,wt=3.5|4000|matter.rune-stone:2;matter.mithril-ingot:1;core.plank:2|rpg.enchanting-table:2|480|slots=hand:1",
        "fantasy|medieval|war-griffin|War griffin|mount|-|spd=120,arm=30,rng=3,rof=30,dmg=70,dt=slash,wt=500|0|-|-|0|cargo=mass:120;fuel=core.grain:40:2;upkeep=core.grain:30;natural",
        "fantasy|medieval|wyvern|Bonded wyvern|mount|fantasy.war-griffin|spd=160,arm=60,rng=30,rof=6,dmg=120,dt=fire,wt=900|0|-|-|0|cargo=mass:200;fuel=core.grain:70:4;upkeep=core.grain:50;natural",
        "fantasy|medieval|sky-galleon|Sky galleon|vehicle-air|-|spd=50,ceil=1500,trv=3000,wt=300000,arm=100,rel=95|40000|naval.oak:2000;naval.sail:8;matter.rune-stone:20;matter.mithril-ingot:40;naval.rigging:4|naval.slipway:3|43200|cargo=mass:30000;crew=piloting:3x40",
        "fantasy|medieval|enchanted-wagon|Enchanted wagon|vehicle-land|medieval.four-horse-wagon|spd=40,trv=300,wt=900,rel=98|9000|core.plank:50;matter.rune-stone:6;core.iron-ingot:12|core.workbench:2|1800|cargo=mass:4000;crew=driving:1x1",
        "fantasy|medieval|rune-hammer|Rune-forged hammer|tool|ancient.dressed-stone-pick|wt=5,eff=600,rel=99|6000|matter.mithril-ingot:3;matter.rune-stone:2;core.labor:5|rpg.forge:4|480|slots=hand:2",
        "fantasy|medieval|siege-golem|Siege golem|structure-unit|medieval.trebuchet|dmg=500,dt=blunt,rng=5,rof=6,arm=700,wt=30000,rel=90|12000|matter.rune-stone:20;core.stone:2000;matter.mithril-ingot:10;core.labor:100|rpg.enchanting-table:3|7200|crew=gunnery:2x1",
        "fantasy|medieval|moon-lantern|Moon lantern|tool|-|wt=1,eff=100,rel=99|2500|matter.moon-crystal:1;matter.mithril-ingot:0.5;core.labor:2|rpg.enchanting-table:1|240|slots=hand:1",
    ];
}
static partial class Data
{
    public static readonly string[] AltMaterials2 =
    [
        "mithril-ingot|Mithril ingot|intermediate|kg|crafted|auto|-|matter.mithril-ore:3.5;core.charcoal:2|rpg.forge:3|1|craft|300",
        "rune-stone|Rune-etched stone|intermediate|{each}|crafted|auto|-|matter.moon-crystal:1;core.clay:2;core.salt:0.1|rpg.enchanting-table:1|1|craft|120",
    ];
}
