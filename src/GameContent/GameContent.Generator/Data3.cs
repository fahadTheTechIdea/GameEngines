static partial class Data
{
    // Ammunition items (matter pack): slug|stats. The material itself is defined in Materials; this says what it does in combat.
    public static readonly string[] AmmoItems =
    [
        "sling-stone|dmg=1,dt=blunt,pen=0,wt=0.05",
        "arrow-flint|dmg=2,dt=pierce,pen=1,wt=0.04",
        "arrow-copper|dmg=3,dt=pierce,pen=2,wt=0.04",
        "arrow-bronze|dmg=4,dt=pierce,pen=3,wt=0.04",
        "arrow-iron|dmg=6,dt=pierce,pen=5,wt=0.04",
        "arrow-bodkin|dmg=9,dt=pierce,pen=12,wt=0.05",
        "bolt|dmg=14,dt=pierce,pen=15,wt=0.1",
        "musket-round|dmg=20,dt=ballistic,pen=10,wt=0.05",
        "round-shot|dmg=80,dt=ballistic,pen=40,wt=3",
        "rifle-round|dmg=14,dt=ballistic,pen=12,wt=0.03",
        "field-shell|dmg=120,dt=blast,pen=60,spl=15,wt=6",
        "energy-mag|dmg=10,dt=energy,pen=20,wt=0.2",
        "plasma-cell|dmg=120,dt=energy,pen=80,spl=3,wt=0.4",
        "autocannon-round|dmg=40,dt=ballistic,pen=30,spl=2,wt=0.4",
        "anti-armor-missile|dmg=300,dt=blast,pen=200,spl=4,wt=12",
    ];

    public static readonly string[] AmmoMaterials =
    [
        "arrow-copper|Copper-tipped arrow|consumable|{each}|crafted|auto|copper|matter.ash-wood:0.1;core.copper-ingot:0.05;matter.sinew:0.02|core.workbench:1|10|craft|30",
        "autocannon-round|Autocannon round|consumable|{each}|crafted|auto|atomic|matter.brass-casing:10;matter.smokeless-powder:0.5;core.steel-ingot:1|core.workbench:2|10|craft|60",
        "anti-armor-missile|Anti-armor guided missile|consumable|{each}|crafted|auto|information|matter.carbon-fibre:2;matter.smokeless-powder:2;electronics.control-board:1|electronics.assembly-line:1|1|assemble|240",
    ];

    // More items. Same layout as Items.
    public static readonly string[] MoreItems =
    [
        // ---------------- copper age
        "ancient|copper|copper-dagger|Copper dagger|weapon|ancient.flint-knife|dmg=7,dt=pierce,rng=1,rof=55,wt=0.3,rel=88|200|core.copper-ingot:0.3;core.wood:0.1|core.furnace:1|30|slots=hand:1",
        "ancient|copper|copper-axe|Copper battle axe|weapon|ancient.stone-axe|dmg=12,dt=slash,rng=1,rof=35,wt=1.3,rel=88|260|core.copper-ingot:0.8;core.wood:0.8|core.furnace:1|50|slots=hand:1",
        "ancient|copper|copper-adze|Copper adze|tool|ancient.hand-axe|wt=1.5,eff=55,rel=88|400|core.copper-ingot:0.6;core.wood:0.4|core.furnace:1|45|slots=hand:1",
        "ancient|copper|hide-coracle|Hide coracle|vehicle-sea|ancient.log-raft|spd=5,trv=30,wt=40,rel=75|300|core.wood:15;matter.cured-hide:6;core.fiber:3|core.workbench:1|300|cargo=mass:250;crew=sailing:1x2",
        "ancient|copper|ox-sledge|Ox-drawn sledge|vehicle-land|ancient.travois|spd=5,trv=25,wt=80,rel=88|500|core.wood:14;matter.cured-hide:3;core.rope:2|core.workbench:1|240|cargo=mass:400;crew=driving:1x1",
        // ---------------- renaissance
        "medieval|renaissance|rapier|Rapier|weapon|medieval.arming-sword|dmg=36,dt=pierce,rng=1,rof=60,wt=1,rel=96|1100|core.steel-ingot:1.3;core.wood:0.2|core.furnace:2|200|slots=hand:1",
        "medieval|renaissance|pike|Pike|weapon|medieval.halberd|dmg=40,dt=pierce,rng=5,rof=20,wt=4.5,rel=95|700|core.steel-ingot:0.5;matter.ash-wood:3|core.workbench:2|120|slots=hand:2",
        "medieval|renaissance|arquebus|Arquebus|weapon|medieval.crossbow|dmg=60,dt=ballistic,rng=80,rof=1,wt=5.5,rel=65|1000|core.steel-ingot:2.4;matter.ash-wood:2;core.fiber:0.5|core.furnace:2|360|slots=hand:2;ammo=matter.musket-round:20",
        "medieval|renaissance|wheel-lock-pistol|Wheel-lock pistol|weapon|-|dmg=50,dt=ballistic,rng=20,rof=1.5,wt=1.4,rel=72|1200|core.steel-ingot:1;matter.ash-wood:0.4;core.bronze-ingot:0.2|core.furnace:2|300|slots=hand:1;ammo=matter.musket-round:6",
        "medieval|renaissance|cuirass-tassets|Cuirass and tassets|armor|medieval.plate-harness|arm=48,wt=14|2800|matter.armor-plate:13;matter.cured-hide:2|core.furnace:2|1200|slots=body:1",
        "medieval|renaissance|morion|Morion helmet|armor|medieval.great-helm|arm=24,wt=1.6|1200|matter.armor-plate:2|core.furnace:2|200|slots=head:1",
        "medieval|renaissance|carrack|Carrack|vehicle-sea|medieval.cog|spd=13,trv=5000,wt=300000,arm=15,rel=85|8000|naval.oak:1500;core.iron-ingot:90;naval.sail:4;naval.rigging:3;naval.anchor:2;naval.pine-tar:120|naval.slipway:2|28800|cargo=mass:200000;crew=sailing:3x60",
        "medieval|renaissance|galleass|Galleass|vehicle-sea|-|spd=10,trv=1800,wt=250000,arm=20,rng=900,dmg=120,dt=ballistic,rof=2,rel=82|7000|naval.oak:1300;core.iron-ingot:80;naval.sail:3;naval.rigging:2;core.rope:100;naval.pine-tar:100|naval.slipway:2|28800|cargo=mass:30000;crew=sailing:2x300",
        "medieval|renaissance|culverin|Culverin|structure-unit|medieval.trebuchet|dmg=180,dt=ballistic,rng=600,rof=1,wt=900,rel=70|2500|core.bronze-ingot:70;core.plank:12;core.iron-ingot:6|core.furnace:3|4800|crew=gunnery:2x4",
        "medieval|renaissance|screw-press|Screw press|tool|-|wt=300,eff=200,rel=90|1500|core.plank:20;core.iron-ingot:8;core.rope:4|core.workbench:2|1200|crew=driving:1x2",
        // ---------------- information
        "modern|information|night-vision|Night-vision goggles|tool|-|wt=0.6,eff=150,rel=92|9000|electronics.lens:2;electronics.cell:1;electronics.control-board:1|electronics.assembly-line:1|120|slots=head:1;fuel=core.electricity:0.05:0.02",
        "modern|information|quadcopter|Quadcopter drone|vehicle-air|modern.helicopter|spd=70,ceil=500,trv=10,wt=1.2,rel=90|7000|matter.carbon-fibre:0.5;electronics.cell:6;electronics.control-board:1;electronics.sensor:1|electronics.assembly-line:1|240|fuel=core.electricity:0.2:0.1",
        "modern|information|modular-carbine|Modular carbine|weapon|modern.assault-rifle|dmg=92,dt=ballistic,rng=500,rof=700,wt=3,rel=96|7000|core.steel-ingot:1.8;mining.aluminium-ingot:0.6;matter.carbon-fibre:0.5|core.furnace:3|420|slots=hand:2;ammo=matter.rifle-round:30",
        "modern|information|armored-utility|Armored utility vehicle|vehicle-land|modern.armored-carrier|dmg=70,dt=ballistic,rng=1200,rof=600,arm=500,spd=100,trv=700,wt=14000,rel=94|40000|core.steel-ingot:260;matter.piston-engine:1;matter.armor-ceramic:20;electronics.control-board:2;core.labor:400|military.vehicle-works:3|21600|cargo=population:6;fuel=mining.diesel:400:40;crew=driving:2x2;ammo=matter.autocannon-round:600",
        "modern|information|atgm-launcher|Anti-armor missile launcher|weapon|-|dmg=60,dt=blast,rng=3000,rof=2,wt=22,rel=93|12000|core.steel-ingot:6;electronics.control-board:2;electronics.sensor:1;matter.carbon-fibre:3|electronics.assembly-line:1|600|slots=hand:2;ammo=matter.anti-armor-missile:2",
        // ---------------- space age
        "future|space|sounding-rocket|Sounding rocket|vehicle-space|-|spd=9000,ceil=150000,trv=300,wt=3000,rel=80|90000|mining.aluminium-ingot:300;electronics.control-board:2;core.labor:900|military.vehicle-works:3|86400|fuel=mining.diesel:2000:600;crew=starflight:2x1",
        "future|space|orbital-shuttle|Orbital shuttle|vehicle-space|future.sounding-rocket|spd=28000,ceil=400000,trv=300000,arm=40,wt=80000,rel=85|700000|mining.aluminium-ingot:6000;matter.titanium-ingot:900;matter.jet-turbine:3;electronics.control-board:20;core.labor:12000|space.shipyard:2|432000|cargo=mass:20000;fuel=mining.diesel:900000:150000;crew=starflight:4x4",
        "future|space|station-module|Space station module|structure-unit|-|arm=80,wt=15000,rel=95|300000|mining.aluminium-ingot:1500;matter.titanium-ingot:300;electronics.power-pack:10;electronics.control-board:6|space.shipyard:2|259200|crew=starflight:2x6",
        "future|space|pressure-suit|Pressure suit|armor|-|arm=20,wt=60|40000|matter.aramid:20;electronics.control-board:2;electronics.power-pack:1;core.cloth:10|electronics.assembly-line:1|1200|slots=body:1;fuel=core.electricity:3:0.5",
        "future|space|lunar-rover|Lunar rover|vehicle-land|-|spd=15,trv=100,wt=700,rel=92|60000|mining.aluminium-ingot:80;matter.titanium-ingot:20;electronics.power-pack:4;electronics.control-board:2|electronics.assembly-line:1|2400|cargo=mass:200;fuel=core.electricity:200:20;crew=driving:2x2",
    ];

    // Formation line: pack|era|slug|name|role|training|strength|equipment item:count;...|crew skill:LxN;...|upkeep res:perday;...|replaces|minutes
    public static readonly string[] Units =
    [
        "ancient|stone|hunting-band|Hunting band|infantry|raw|8|ancient.stone-axe:4;ancient.hunting-bow:4|-|core.bread:8|-|2880",
        "ancient|copper|copper-axe-band|Copper axe band|infantry|raw|12|ancient.copper-axe:8;ancient.copper-dagger:4|-|core.bread:12|ancient.hunting-band|3600",
        "ancient|bronze|spear-levy|Spear levy|infantry|regular|20|ancient.bronze-spear:20;ancient.wicker-shield:20|-|core.bread:20|ancient.copper-axe-band|5760",
        "ancient|iron|iron-warband|Iron warband|infantry|regular|24|ancient.iron-spear:12;ancient.iron-blade:12;ancient.bossed-shield:24;ancient.iron-helmet:24|-|core.bread:24|ancient.spear-levy|5760",
        "ancient|classical|heavy-cohort|Heavy cohort|infantry|veteran|40|ancient.stabbing-sword:40;ancient.tower-shield:40;ancient.segmented-cuirass:40;ancient.heavy-javelin:40|-|core.bread:40|ancient.iron-warband|8640",
        "ancient|classical|catapult-battery|Catapult battery|artillery|regular|6|ancient.torsion-catapult:1|gunnery:1x4|core.bread:6|-|4320",
        "medieval|medieval|longbow-company|Longbow company|missile|regular|60|medieval.longbow:60|-|core.bread:60|-|11520",
        "medieval|medieval|men-at-arms|Men-at-arms|infantry|veteran|30|medieval.arming-sword:30;medieval.plate-harness:30;medieval.heater-shield:30|-|core.bread:60|ancient.heavy-cohort|11520",
        "medieval|renaissance|pike-and-shot|Pike and shot|infantry|regular|90|medieval.pike:50;medieval.arquebus:40|-|core.bread:90|medieval.men-at-arms|14400",
        "medieval|gunpowder|musket-company|Musket company|infantry|regular|80|medieval.flintlock-musket:80;medieval.bayonet:80|-|core.bread:80|medieval.pike-and-shot|14400",
        "medieval|gunpowder|field-battery|Field battery|artillery|veteran|30|medieval.bronze-cannon:4|gunnery:1x16|core.bread:30|-|14400",
        "modern|industrial|rifle-company|Rifle company|infantry|regular|100|modern.repeating-rifle:100|-|core.bread:100|medieval.musket-company|17280",
        "modern|modern|rifle-platoon|Rifle platoon|infantry|regular|36|modern.bolt-rifle:30;modern.light-machine-gun:6|-|core.bread:36|modern.rifle-company|8640",
        "modern|modern|tank-platoon|Tank platoon|armour|regular|16|modern.medium-tank:4|driving:1x4|core.bread:16|-|20160",
        "modern|atomic|mechanized-platoon|Mechanized platoon|infantry|veteran|36|modern.assault-rifle:30;modern.armored-carrier:4|driving:1x8|core.bread:36|modern.rifle-platoon|17280",
        "modern|atomic|fighter-flight|Fighter flight|air|veteran|8|modern.jet-fighter:4|piloting:2x4|core.bread:8|-|28800",
        "modern|information|drone-team|Drone team|air|regular|6|modern.quadcopter:6|piloting:1x6|core.bread:6|-|4320",
        "future|near|exo-squad|Exo-frame squad|infantry|elite|10|future.exo-frame:10;future.railgun-carbine:10|-|core.bread:20|modern.mechanized-platoon|20160",
        "future|far|plasma-squad|Plasma assault squad|infantry|elite|10|future.nanoweave-suit:10;future.plasma-rifle:10|-|core.bread:20|future.exo-squad|20160",
        "future|far|starfighter-wing|Starfighter wing|air|elite|12|future.starfighter:12|starflight:3x12|core.bread:24|-|43200",
        "fantasy|medieval|mithril-guard|Mithril guard|infantry|elite|20|fantasy.mithril-longsword:20;fantasy.mithril-mail:20|-|core.bread:40|-|17280",
        "fantasy|medieval|griffin-riders|Griffin riders|cavalry|veteran|8|fantasy.war-griffin:8;fantasy.elder-bow:8|riding:2x8|core.bread:16|-|20160",
    ];
}
