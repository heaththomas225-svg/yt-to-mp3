using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

// The emoji panel of the lyrics writer: search by word (fire, heart, guitar, sad...), pick a group, click an emoji to put it in the lyrics where the cursor is.
static class EmojiPanel
{
    public sealed class Item { public string E, Words; }
    // group name -> "emoji words;emoji words;..."
    static readonly string[][] Groups = {
        new[] { "Faces", "😀 grin happy smile;😃 smile happy open;😄 smile happy laugh;😁 beam grin teeth;😆 laugh squint;😅 sweat laugh nervous;🤣 rofl rolling laugh lol;😂 joy tears laugh lol;🙂 slight smile;🙃 upside down silly;😉 wink;😊 blush smile happy;😇 angel halo innocent;🥰 love hearts adore;😍 heart eyes love;🤩 star struck wow amazing;😘 kiss blow love;😗 kiss;😚 kiss closed eyes;😋 yum tasty delicious;😛 tongue silly;😜 wink tongue crazy;🤪 zany crazy wild;😝 tongue squint;🤑 money rich cash;🤗 hug;🤭 giggle oops;🤫 shh quiet secret;🤔 think hmm;🤐 zip mouth quiet;🤨 eyebrow suspicious;😐 neutral;😑 expressionless;😶 no mouth silent;😏 smirk;😒 unamused annoyed;🙄 eye roll;😬 grimace awkward;😌 relieved calm;😔 pensive sad;😪 sleepy;😴 sleep tired zzz;😷 mask sick;🤒 sick fever;🤕 hurt bandage;🤢 sick nausea;🤮 vomit;🥵 hot sweat;🥶 cold freezing;🥴 woozy drunk;😵 dizzy;🤯 mind blown explode;🤠 cowboy;🥳 party celebrate birthday;😎 cool sunglasses;🤓 nerd;🧐 monocle;😕 confused;😟 worried;🙁 frown sad;☹️ frown sad;😮 surprised wow open mouth;😯 hushed;😲 astonished;😳 flushed embarrassed;🥺 pleading puppy eyes please;😦 frown open;😨 fear scared;😰 anxious sweat;😥 sad relieved;😢 cry sad tear;😭 sob crying loud sad;😱 scream fear shock;😖 confounded;😣 persevere;😞 disappointed sad;😓 downcast sweat;😩 weary tired;😫 tired;🥱 yawn bored;😤 triumph steam angry;😡 angry mad red;😠 angry;🤬 swearing cursing angry;😈 devil smile evil;👿 devil angry;💀 skull dead death;☠️ skull crossbones danger;💩 poop;🤡 clown;👻 ghost;👽 alien;🤖 robot;😺 cat smile;😸 cat grin;😹 cat joy;😻 cat love;😼 cat smirk;🙈 see no evil monkey;🙉 hear no evil monkey;🙊 speak no evil monkey" },
        new[] { "Hands", "👍 thumbs up yes good like;👎 thumbs down no bad;👌 ok perfect;✌️ peace victory;🤞 fingers crossed luck;🤟 love you rock;🤘 rock on metal horns;🤙 call me shaka;👈 point left;👉 point right;👆 point up;👇 point down;☝️ one up;✋ stop hand high five;🤚 raised back hand;🖐️ hand fingers;🖖 vulcan spock;👋 wave hello bye;🤝 handshake deal;👏 clap applause bravo;🙌 raised hands praise hooray;👐 open hands;🤲 palms up;🙏 pray please thank you;✊ fist power;👊 punch fist bump;🤛 fist left;🤜 fist right;💪 strong muscle flex;🦾 robot arm;✍️ writing hand;💅 nails polish;🤳 selfie;🦶 foot;👂 ear listen;👃 nose;👀 eyes look;👁️ eye;👅 tongue;👄 mouth lips;🧠 brain mind;🫶 heart hands love;🫡 salute;🫠 melting" },
        new[] { "Hearts", "❤️ red heart love;🧡 orange heart;💛 yellow heart;💚 green heart;💙 blue heart;💜 purple heart;🖤 black heart dark;🤍 white heart;🤎 brown heart;💔 broken heart sad heartbreak;❣️ heart exclamation;💕 two hearts love;💞 revolving hearts;💓 beating heart;💗 growing heart;💖 sparkling heart;💘 heart arrow cupid;💝 heart ribbon gift;💟 heart decoration;♥️ heart suit;😍 heart eyes;🥰 smiling hearts;😘 blow kiss;💋 kiss lips;💌 love letter;💍 ring wedding;💑 couple love;💏 kiss couple;🌹 rose flower love;🥀 wilted rose;💐 bouquet" },
        new[] { "Music", "🎵 note music song;🎶 notes music melody;🎼 score sheet music;🎤 microphone sing vocal mic;🎧 headphones listen;🎙️ studio microphone podcast;🎚️ fader slider mixer;🎛️ knobs dials mixer;📻 radio;🔊 speaker loud volume;🔉 speaker medium;🔈 speaker low;🔇 mute silent;🔔 bell;🎸 guitar rock;🪕 banjo;🎹 piano keyboard keys;🎷 saxophone sax jazz;🎺 trumpet;🎻 violin;🪗 accordion;🥁 drum drums beat;🪘 long drum;🪇 maracas;🪈 flute;📀 dvd disc;💿 cd disc record;📼 tape cassette;📺 tv;🎬 clapper film movie;🎞️ film;🎥 camera movie video;📹 video camera;🎭 theatre masks drama;🎪 circus tent;🎠 carousel;🎡 ferris wheel;🪩 disco ball party dance;💃 dance woman dancing;🕺 dance man dancing;👯 dancers party;🎉 party popper celebrate;🎊 confetti ball party;🎈 balloon;🎆 fireworks;🎇 sparkler;🏆 trophy winner champion;🥇 gold medal first;🥈 silver medal second;🥉 bronze medal third;🏅 medal;🎖️ military medal;👑 crown king queen;💎 diamond gem;📣 megaphone loud;📢 loudspeaker announce;🔥 fire hot lit;💯 hundred perfect;⭐ star;🌟 glowing star;✨ sparkles shine;💫 dizzy star;⚡ lightning bolt power energy;💥 boom explosion collision;🚀 rocket launch fast" },
        new[] { "Fire & sky", "🔥 fire hot lit flame;💥 boom explosion;✨ sparkles shine glitter;⭐ star;🌟 glowing star;💫 dizzy shooting star;⚡ lightning bolt thunder;☄️ comet;🌈 rainbow;☀️ sun sunny;🌤️ sun cloud;⛅ partly cloudy;☁️ cloud;🌧️ rain;⛈️ storm thunder;🌩️ lightning cloud;❄️ snowflake cold winter;☃️ snowman;⛄ snowman;🌬️ wind;💨 dash wind fast;💧 droplet water tear;💦 sweat splash water;🌊 wave ocean sea;🌙 moon night crescent;🌛 moon face;🌕 full moon;🌑 new moon;🌌 milky way galaxy night;🌠 shooting star wish;🪐 saturn planet;🌍 earth globe europe;🌎 earth americas;🌏 earth asia;🌋 volcano;🏔️ mountain;🌅 sunrise;🌄 sunrise mountains;🌇 sunset city;🌃 night city stars;🌆 city dusk;🌉 bridge night;🌁 foggy" },
        new[] { "Nature", "🌹 rose;🌷 tulip;🌸 cherry blossom;🌺 hibiscus;🌻 sunflower;🌼 daisy blossom;💐 bouquet;🌱 seedling;🌿 herb leaf;☘️ shamrock clover;🍀 four leaf clover luck;🍁 maple leaf autumn;🍂 fallen leaves autumn;🍃 leaf wind;🌴 palm tree beach;🌳 tree;🌲 evergreen pine;🌵 cactus desert;🌾 rice grain;🍄 mushroom;🪨 rock stone;🪵 wood log;🌐 globe;🏝️ island beach;🏜️ desert;🏞️ park nature;🌅 sunrise" },
        new[] { "Animals", "🐶 dog puppy;🐱 cat kitten;🐭 mouse;🐹 hamster;🐰 rabbit bunny;🦊 fox;🐻 bear;🐼 panda;🐨 koala;🐯 tiger;🦁 lion;🐮 cow;🐷 pig;🐸 frog;🐵 monkey;🐔 chicken;🐧 penguin;🐦 bird;🐤 chick;🦆 duck;🦅 eagle;🦉 owl;🦇 bat;🐺 wolf;🐗 boar;🐴 horse;🦄 unicorn magic;🐝 bee;🦋 butterfly;🐌 snail;🐞 ladybug;🐜 ant;🕷️ spider;🦂 scorpion;🐢 turtle;🐍 snake;🦎 lizard;🐙 octopus;🦑 squid;🦀 crab;🐠 fish tropical;🐟 fish;🐬 dolphin;🐳 whale;🦈 shark;🐊 crocodile;🐅 tiger;🐆 leopard;🦓 zebra;🦍 gorilla;🐘 elephant;🦏 rhino;🐪 camel;🦒 giraffe;🦘 kangaroo;🐕 dog;🐈 cat;🐈‍⬛ black cat;🕊️ dove peace;🦚 peacock;🦜 parrot;🦢 swan;🦩 flamingo;🐉 dragon;🐲 dragon face;🦖 t rex dinosaur;🦕 dinosaur;🐾 paw prints;🐺 wolf howl" },
        new[] { "Food & drink", "🍎 apple red;🍏 green apple;🍊 orange;🍋 lemon;🍌 banana;🍉 watermelon;🍇 grapes;🍓 strawberry;🫐 blueberries;🍒 cherries;🍑 peach;🥭 mango;🍍 pineapple;🥥 coconut;🥝 kiwi;🍅 tomato;🥑 avocado;🌽 corn;🌶️ chilli hot pepper;🥕 carrot;🍞 bread;🥐 croissant;🧀 cheese;🍳 egg cooking;🥓 bacon;🍔 burger;🍟 fries;🍕 pizza;🌭 hot dog;🌮 taco;🌯 burrito;🍝 pasta spaghetti;🍜 noodles ramen;🍣 sushi;🍩 donut;🍪 cookie;🎂 cake birthday;🍰 cake slice;🍫 chocolate;🍬 sweet candy;🍭 lollipop;🍦 ice cream;☕ coffee tea hot;🍵 tea green;🍺 beer;🍻 cheers beers;🥂 champagne cheers toast;🍷 wine;🥃 whisky tumbler;🍸 cocktail martini;🍹 tropical drink;🍾 champagne bottle pop;🥤 soda cup;🧃 juice box;🥛 milk;🧊 ice;🍼 baby bottle" },
        new[] { "Travel & places", "🚗 car;🚕 taxi;🚌 bus;🏎️ racing car fast;🚓 police car;🚑 ambulance;🚒 fire truck;🚚 truck;🚲 bike bicycle;🛵 scooter;🏍️ motorbike;🚂 train;🚄 fast train;✈️ plane flight;🛫 takeoff;🛬 landing;🚁 helicopter;🚀 rocket;🛸 ufo flying saucer;⛵ sailboat;🚢 ship;🏠 house home;🏡 house garden;🏢 office building;🏙️ city skyline;🌆 city dusk;🗽 statue of liberty new york;🗼 tower tokyo;🏰 castle;🎡 ferris wheel;🏖️ beach;⛰️ mountain;🗺️ map world;🧭 compass;⛽ fuel;🚦 traffic light;🛣️ road motorway;🌉 bridge;⚓ anchor;🏟️ stadium;🎢 roller coaster;⛺ camping tent" },
        new[] { "Objects", "📱 phone mobile;☎️ telephone;📞 receiver call;💻 laptop computer;🖥️ desktop computer;⌨️ keyboard;🖱️ mouse;💾 floppy save;📷 camera;📸 camera flash;🔦 torch flashlight;💡 light bulb idea;🔋 battery;🔌 plug;📚 books;📖 open book;📝 memo note write;✏️ pencil;🖊️ pen;📌 pin;📎 paperclip;✂️ scissors;🔑 key;🔒 lock;🔓 unlock;🛠️ tools;🔧 wrench;🔨 hammer;⚙️ gear settings;🧲 magnet;💣 bomb;🔫 water pistol;🗡️ dagger;🛡️ shield;💰 money bag;💵 dollar cash;💶 euro;💷 pound;💳 credit card;🛒 shopping cart;🎁 gift present;🎀 ribbon bow;🧸 teddy bear;🕶️ sunglasses cool;👓 glasses;👟 sneakers trainers shoe;👠 high heel;👜 handbag;🎒 backpack;👕 t shirt;👖 jeans;🧢 cap hat;🎩 top hat;👑 crown;💄 lipstick;💍 ring;⌚ watch time;⏰ alarm clock;⏳ hourglass time;🕰️ clock;📅 calendar;📈 chart up growth;📉 chart down;📊 bar chart;🏁 chequered flag finish race;🚬 cigarette;⚰️ coffin;🗿 moai statue;🪞 mirror;🪑 chair;🛏️ bed;🚪 door;🪟 window;🛋️ sofa;🚿 shower;🧻 toilet paper" },
        new[] { "Symbols", "✅ check tick yes;❌ cross no;❎ cross mark;⭕ circle;🛑 stop;⛔ no entry;🚫 prohibited banned;❗ exclamation;❓ question;‼️ double exclamation;⁉️ exclamation question;⚠️ warning;♻️ recycle;💯 hundred;🔴 red circle;🟠 orange circle;🟡 yellow circle;🟢 green circle;🔵 blue circle;🟣 purple circle;⚫ black circle;⚪ white circle;🔺 triangle red up;🔻 triangle red down;🔶 orange diamond;🔷 blue diamond;▶️ play;⏸️ pause;⏹️ stop button;⏺️ record;⏭️ next track;⏮️ previous track;⏩ fast forward;⏪ rewind;🔀 shuffle;🔁 repeat;🔂 repeat one;➕ plus;➖ minus;✖️ multiply;➗ divide;➡️ right arrow;⬅️ left arrow;⬆️ up arrow;⬇️ down arrow;↗️ up right;🔄 refresh cycle;🔃 reload;🔝 top;🆗 ok;🆒 cool;🆕 new;🆓 free;🔞 eighteen;©️ copyright;®️ registered;™️ trademark;♾️ infinity forever;💲 dollar sign;➰ curly loop;〰️ wavy dash;☮️ peace sign;☯️ yin yang;✝️ cross;☪️ star crescent;🕉️ om;♈ aries;♉ taurus;♊ gemini;♋ cancer;♌ leo;♍ virgo;♎ libra;♏ scorpio;♐ sagittarius;♑ capricorn;♒ aquarius;♓ pisces" },
        new[] { "Flags", "🏳️ white flag surrender;🏴 black flag;🏁 chequered flag;🚩 red flag;🏳️‍🌈 rainbow pride flag;🏴‍☠️ pirate flag;🇬🇧 united kingdom uk britain england;🇺🇸 usa united states america;🇨🇦 canada;🇦🇺 australia;🇳🇿 new zealand;🇮🇪 ireland;🇫🇷 france;🇩🇪 germany;🇪🇸 spain;🇮🇹 italy;🇵🇹 portugal;🇳🇱 netherlands holland;🇧🇪 belgium;🇸🇪 sweden;🇳🇴 norway;🇩🇰 denmark;🇫🇮 finland;🇵🇱 poland;🇺🇦 ukraine;🇷🇺 russia;🇹🇷 turkey;🇬🇷 greece;🇮🇳 india;🇵🇰 pakistan;🇨🇳 china;🇯🇵 japan;🇰🇷 south korea korea;🇧🇷 brazil;🇦🇷 argentina;🇲🇽 mexico;🇨🇴 colombia;🇨🇺 cuba;🇯🇲 jamaica;🇳🇬 nigeria;🇬🇭 ghana;🇿🇦 south africa;🇰🇪 kenya;🇪🇬 egypt;🇲🇦 morocco;🇸🇦 saudi arabia;🇦🇪 uae emirates dubai;🇮🇱 israel;🇵🇭 philippines;🇹🇭 thailand;🇻🇳 vietnam;🇮🇩 indonesia" },
    };
    static List<Item> all; static readonly List<string> recent = new List<string>();
    public static List<Item> Group(int g)
    {
        var l = new List<Item>(); foreach (var part in Groups[g][1].Split(';')) { int i = part.IndexOf(' '); if (i < 1) continue; l.Add(new Item { E = part.Substring(0, i), Words = part.Substring(i + 1).ToLowerInvariant() }); } return l;
    }
    public static string[] GroupNames { get { return Groups.Select(x => x[0]).ToArray(); } }
    public static List<Item> All()
    {
        if (all == null) { all = new List<Item>(); var seen = new HashSet<string>(); for (int g = 0; g < Groups.Length; g++) foreach (var it in Group(g)) if (seen.Add(it.E)) all.Add(it); }
        return all;
    }
    public static int Count { get { return All().Count; } }
    // every word typed has to be in the words of the emoji (or be the emoji itself)
    public static List<Item> Search(string q)
    {
        q = (q ?? "").Trim().ToLowerInvariant(); if (q.Length == 0) return new List<Item>(); var words = q.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
        return All().Where(it => it.E == q || words.All(w => it.Words.Contains(w))).ToList();
    }
    static string RecentFile { get { return Path.Combine(StudioEnv.DataDir, "emoji-recent.txt"); } }
    public static List<Item> Recent()
    {
        if (recent.Count == 0) { try { if (File.Exists(RecentFile)) foreach (var l in File.ReadAllLines(RecentFile, Encoding.UTF8)) if (l.Trim().Length > 0 && recent.Count < 24) recent.Add(l.Trim()); } catch { } }
        var map = All().ToDictionary(x => x.E, x => x); var res = new List<Item>(); foreach (var e in recent) { Item it; if (map.TryGetValue(e, out it)) res.Add(it); } return res;
    }
    public static void Used(string e)
    {
        Recent(); recent.Remove(e); recent.Insert(0, e); if (recent.Count > 24) recent.RemoveRange(24, recent.Count - 24);
        try { Directory.CreateDirectory(StudioEnv.DataDir); File.WriteAllText(RecentFile, string.Join("\n", recent.ToArray()), Encoding.UTF8); } catch { }
    }

    // the panel: a tool window next to the lyrics; each click puts one emoji in (insert is how the lyrics box takes it)
    static Form open;
    public static void Show(Form owner, Action<string> insert)
    {
        if (open != null && !open.IsDisposed) { open.Activate(); return; }
        var Fld = TM.C(255, 34, 16, 62); var Ink = TM.C(255, 240, 232, 255);
        var f = new Form { Text = "Emojis", FormBorderStyle = FormBorderStyle.FixedToolWindow, StartPosition = FormStartPosition.Manual, ShowInTaskbar = false, BackColor = TM.C(255, 24, 12, 44), ForeColor = Ink, ClientSize = new Size(480, 560), Font = new Font("Segoe UI", 10f), TopMost = false };
        Win.Dark(f); open = f; f.FormClosed += (s, e) => open = null; f.Owner = owner;
        var wa = Screen.FromControl(owner).WorkingArea; f.Location = new Point(Math.Min(wa.Right - f.Width - 10, owner.Right - f.Width - 30), Math.Max(wa.Top + 10, owner.Top + 90));
        var q = new TextBox { Left = 12, Top = 12, Width = 456, BackColor = Fld, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 12f) }; Win.NeonBox(q); f.Controls.Add(q);
        var hint = new Label { Left = 12, Top = 42, Width = 456, Height = 20, ForeColor = TM.C(255, 160, 140, 200), Text = "Search by word: fire, heart, guitar, sad, party... or pick a group. Click an emoji to put it in your lyrics." }; f.Controls.Add(hint);
        var names = GroupNames; var tabs = new List<Button>(); int cur = -2;   // -2 recent, -1 search, 0.. groups
        var view = new Panel { Left = 12, Top = 150, Width = 456, Height = 398, BackColor = TM.C(255, 14, 7, 30) }; Win.NeonBox(view); f.Controls.Add(view);
        var inner = new Panel { Left = 0, Top = 0, Width = 456 - NeonBar.BarW - 8, Height = 398, BackColor = TM.C(255, 14, 7, 30) }; view.Controls.Add(inner);
        typeof(Panel).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(inner, true, null);
        var bar = new NeonPanelScroll(view, inner); List<Item> items = new List<Item>(); const int Cell = 46; int hot = -1;
        Action<int> layout = null; Action fill = null;
        Func<int, string> title = i => i == -2 ? "Recently used" : (i == -1 ? "Search" : names[i]);
        fill = () =>
        {
            if (cur == -1) items = Search(q.Text); else if (cur == -2) items = Recent(); else items = Group(cur);
            int cols = Math.Max(1, inner.Width / Cell), rows = Math.Max(1, (items.Count + cols - 1) / cols); inner.Height = Math.Max(view.ClientSize.Height, rows * Cell + 8); inner.Top = 0; inner.Invalidate(); hot = -1;
            hint.Text = cur == -1 ? (q.Text.Trim().Length == 0 ? "Type a word to search all " + Count + " emojis." : items.Count + " found for \"" + q.Text.Trim() + "\".") : (cur == -2 && items.Count == 0 ? "The emojis you use show here. Pick a group below." : title(cur) + ": " + items.Count + " emojis. Click one to put it in your lyrics.");
            foreach (var b in tabs) b.BackColor = (int)b.Tag == cur ? TM.C(255, 150, 70, 230) : TM.C(255, 56, 30, 100);
        };
        int tx = 12, ty = 70; for (int i = -2; i < names.Length; i++)
        {
            if (i == -1) continue; int gi = i; string label = i == -2 ? "RECENT" : names[i].ToUpper(); int w = 22 + label.Length * 8; if (tx + w > 468) { tx = 12; ty += 36; }
            var b = new PxButton { Text = label, Left = tx, Top = ty, Width = w, Height = 30, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, Cursor = Cursors.Hand, Font = new Font("Segoe UI Semibold", 8.5f), Tag = i }; b.FlatAppearance.BorderColor = TM.C(255, 190, 120, 255);
            b.Click += (s, e) => { cur = gi; q.Text = ""; fill(); }; f.Controls.Add(b); tabs.Add(b); tx += w + 6;
        }
        view.Top = ty + 40; view.Height = 548 - view.Top; inner.Height = view.Height;
        inner.Paint += (s, e) =>
        {
            var g = e.Graphics; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; int cols = Math.Max(1, inner.Width / Cell);
            for (int i = 0; i < items.Count; i++)
            {
                var r = new RectangleF((i % cols) * Cell + 3, (i / cols) * Cell + 4, Cell - 6, Cell - 6); if (r.Bottom < -inner.Top - 2 || r.Y > -inner.Top + view.Height + 2) continue;
                using (var p = Rr(r, 9)) { using (var br = new SolidBrush(i == hot ? TM.C(255, 118, 52, 212) : TM.C(255, 28, 15, 56))) g.FillPath(br, p); using (var pen = new Pen(i == hot ? Color.White : TM.C(120, 150, 110, 220), i == hot ? 2f : 1f)) g.DrawPath(pen, p); }
                EmojiText.Draw(g, items[i].E, 22, Color.White, r, StringAlignment.Center, false);
            }
            if (items.Count == 0 && cur != -2) using (var br = new SolidBrush(TM.C(255, 165, 148, 190))) g.DrawString(cur == -1 && q.Text.Trim().Length > 0 ? "Nothing matches that word." : "", f.Font, br, 10, 10);
        };
        Func<Point, int> at = pt => { int cols = Math.Max(1, inner.Width / Cell); int c = pt.X / Cell, r = (pt.Y - 4) / Cell; int i = r * cols + c; return c < cols && pt.Y >= 4 && i >= 0 && i < items.Count ? i : -1; };
        var tip = new ToolTip();
        inner.MouseMove += (s, e) => { int i = at(e.Location); if (i != hot) { hot = i; inner.Invalidate(); tip.SetToolTip(inner, i >= 0 ? items[i].Words.Split(' ')[0] : ""); } };
        inner.MouseLeave += (s, e) => { hot = -1; inner.Invalidate(); };
        inner.MouseDown += (s, e) => { int i = at(e.Location); if (i < 0 || e.Button != MouseButtons.Left) return; string em = items[i].E; Used(em); insert(em); if (cur == -2) { } };
        q.TextChanged += (s, e) => { cur = q.Text.Trim().Length > 0 ? -1 : (cur == -1 ? 0 : cur); fill(); };
        cur = Recent().Count > 0 ? -2 : 0; f.Shown += (s, e) => { fill(); q.Focus(); }; f.Show(owner);
    }
    static System.Drawing.Drawing2D.GraphicsPath Rr(RectangleF r, float rad) { var p = new System.Drawing.Drawing2D.GraphicsPath(); float d = rad * 2; p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p; }
}
