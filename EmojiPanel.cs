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

    // the panel: a tool window next to the lyrics. The emojis are drawn by the browser engine that is part of Windows, so they are in COLOUR (the normal drawing code of the program can only write them in one colour).
    // Each click puts one emoji in the lyrics where the cursor is.
    static Form open; public static WebBrowser LastBrowser;
    static string Hex(Color c) { return "#" + c.R.ToString("x2") + c.G.ToString("x2") + c.B.ToString("x2"); }
    static string Esc(string t) { return t.Replace("&", "&amp;").Replace("<", "&lt;").Replace("\"", "&quot;"); }
    // the page: a search box, the groups, the emojis in a grid with a scroll bar in the neon style, and a few lines of script (ES5: it is shown by Internet Explorer's engine)
    public static string BuildPage()
    {
        var th = Themes.Current; string violet = Hex(th.Violet), cyan = Hex(th.Cyan), pink = Hex(th.Pink), bg = Hex(th.Bg0), panel = Hex(th.Panel);
        var sb = new StringBuilder(); var names = GroupNames; var recent = Recent();
        sb.Append("<!DOCTYPE html><html><head><meta http-equiv='X-UA-Compatible' content='IE=edge'><meta charset='utf-8'><style>");
        sb.Append("html,body{margin:0;height:100%;overflow:hidden;background:" + bg + ";font-family:'Segoe UI',sans-serif;color:#f0e8ff;-ms-user-select:none;user-select:none;cursor:default}");
        sb.Append("#top{padding:10px 12px 0 12px}#q{width:100%;box-sizing:border-box;background:#0a0516;border:1px solid " + cyan + ";border-radius:8px;color:#fff;font-size:15px;padding:7px 10px;outline:none;font-family:'Segoe UI'}");
        sb.Append("#hint{font-size:12px;color:#a99dd8;margin:6px 2px 8px 2px;height:16px}#tabs{padding:0 12px}.t{display:inline-block;margin:0 5px 6px 0;padding:6px 12px;border:1px solid " + violet + ";border-radius:10px;background:#1c0f3a;font-size:11.5px;font-weight:600;letter-spacing:.4px;text-transform:uppercase;cursor:pointer}.t:hover{background:#2c1858;border-color:" + cyan + "}.t.on{background:" + pink + ";color:#fff;border-color:#fff}");
        sb.Append("#wrap{position:absolute;left:12px;right:12px;top:0;bottom:12px;border:1px solid " + violet + ";border-radius:12px;background:#0e0720}#grid{position:absolute;left:0;top:0;right:26px;bottom:0;overflow-y:scroll;-ms-overflow-style:none;padding:6px 4px 6px 8px}");
        sb.Append(".e{display:inline-block;width:44px;height:44px;margin:3px;border:1px solid #3a2570;border-radius:10px;background:#190c34;font-family:'Segoe UI Emoji','Segoe UI Symbol';font-size:26px;line-height:42px;text-align:center;cursor:pointer;vertical-align:top}.e:hover{background:#2a1658;border-color:#fff}.e:active{background:" + violet + "}");
        sb.Append("#bar{position:absolute;right:5px;top:6px;bottom:6px;width:16px;border:1px solid " + violet + ";border-radius:8px;background:#06030f}#th{position:absolute;left:1px;right:1px;top:0;height:40px;border-radius:7px;background:" + violet + ";border:1px solid " + cyan + ";cursor:pointer}#th:hover{background:" + pink + "}");
        sb.Append("#none{padding:16px;color:#a99dd8;font-size:13px}</style></head><body><div id='top'><input id='q' type='text' autocomplete='off' placeholder='Search by word: fire, heart, guitar, sad, party...'><div id='hint'></div></div><div id='tabs'></div><div id='wrap'><div id='grid'>");
        for (int g = 0; g < names.Length; g++) { if (names[g] == "Flags") continue; foreach (var it in Group(g)) sb.Append("<span class='e' data-g='" + g + "' data-w=\"" + Esc(it.Words) + "\" onclick='pick(this)'>" + it.E + "</span>"); }
        sb.Append("<div id='none' style='display:none'>Nothing matches that word.</div></div><div id='bar'><div id='th'></div></div></div>");
        sb.Append("<script>var names=[");
        for (int g = 0; g < names.Length; g++) { if (names[g] == "Flags") continue; sb.Append((g > 0 ? "," : "") + "[" + g + ",\"" + names[g].ToUpper().Replace("\"", "") + "\"]"); }
        sb.Append("];var recent=[");
        for (int r = 0; r < recent.Count; r++) sb.Append((r > 0 ? "," : "") + "\"" + recent[r].E + "\"");
        sb.Append("];var cur=" + (recent.Count > 0 ? "-2" : "0") + ";");
        sb.Append(@"
var grid=document.getElementById('grid'),q=document.getElementById('q'),hint=document.getElementById('hint'),th=document.getElementById('th'),bar=document.getElementById('bar'),none=document.getElementById('none'),tabs=document.getElementById('tabs');
var items=grid.getElementsByClassName('e');
function mk(label,id){var d=document.createElement('span');d.className='t';d.setAttribute('data-id',id);d.innerHTML=label;d.onclick=function(){q.value='';cur=id;show();};tabs.appendChild(d);}
mk('RECENT',-2);for(var i=0;i<names.length;i++)mk(names[i][1],names[i][0]);
function words(){return q.value.replace(/^\s+|\s+$/g,'').toLowerCase().split(/[\s,]+/);}
function show(){var w=words(),searching=q.value.replace(/\s/g,'')!='',n=0,i,j,ok;
 var seen={};for(i=0;i<items.length;i++){var it=items[i],g=parseInt(it.getAttribute('data-g'),10);
  if(searching){ok=true;var t=it.getAttribute('data-w')+' '+it.textContent;for(j=0;j<w.length;j++)if(w[j]!==''&&t.indexOf(w[j])<0){ok=false;break;}}
  else if(cur==-2){ok=false;for(j=0;j<recent.length;j++)if(recent[j]==it.textContent){ok=true;break;}}
  else ok=(g==cur);
  if(ok&&searching){if(seen[it.textContent])ok=false;else seen[it.textContent]=1;}
  it.style.display=ok?'inline-block':'none';if(ok)n++;}
 none.style.display=n==0?'block':'none';none.innerHTML=searching?'Nothing matches that word.':(cur==-2?'The emojis you use show here. Pick a group above.':'');
 var ts=tabs.getElementsByClassName('t');for(i=0;i<ts.length;i++)ts[i].className='t'+((!searching&&ts[i].getAttribute('data-id')==cur)?' on':'');
 hint.innerHTML=searching?(n+' found'):(cur==-2?'Recently used':'Click an emoji to put it in your lyrics');grid.scrollTop=0;fit();}
function pick(el){var e=el.textContent;try{window.external.Pick(e);}catch(x){}var r=[e],j;for(j=0;j<recent.length;j++)if(recent[j]!=e&&r.length<24)r.push(recent[j]);recent=r;}
function fit(){var h=grid.clientHeight,c=grid.scrollHeight;if(c<=h+1){bar.style.visibility='hidden';return;}bar.style.visibility='visible';var bh=bar.clientHeight,t=Math.max(30,Math.round(bh*h/c));th.style.height=t+'px';th.style.top=Math.round((bh-t)*grid.scrollTop/(c-h))+'px';}
grid.onscroll=fit;window.onresize=function(){wrapFit();fit();};
function wrapFit(){var w=document.getElementById('wrap'),top=document.getElementById('top').offsetHeight+tabs.offsetHeight+6;w.style.top=top+'px';}
var dragging=false,startY=0,startTop=0;
th.onmousedown=function(e){e=e||window.event;dragging=true;startY=e.clientY;startTop=grid.scrollTop;return false;};
document.onmousemove=function(e){if(!dragging)return;e=e||window.event;var h=grid.clientHeight,c=grid.scrollHeight,bh=bar.clientHeight,t=th.offsetHeight;grid.scrollTop=startTop+(e.clientY-startY)*(c-h)/Math.max(1,bh-t);return false;};
document.onmouseup=function(){dragging=false;};
bar.onmousedown=function(e){e=e||window.event;if(e.srcElement===th)return;var r=th.getBoundingClientRect();grid.scrollTop+=(e.clientY<r.top?-1:1)*grid.clientHeight*0.9;};
q.onkeyup=show;q.oninput=show;document.onselectstart=function(){return event.srcElement==q;};document.oncontextmenu=function(){return false;};
function setQuery(v){q.value=v;show();}
window.onload=function(){wrapFit();show();q.focus();};
</script></body></html>");
        return sb.ToString();
    }
    public static void Show(Form owner, Action<string> insert)
    {
        if (open != null && !open.IsDisposed) { if (open.Owner == owner) { open.Activate(); return; } open.Close(); }   // a panel opened for another window is closed: the emojis go where the new one is
        var f = new Form { Text = "Emojis", FormBorderStyle = FormBorderStyle.FixedToolWindow, StartPosition = FormStartPosition.Manual, ShowInTaskbar = false, BackColor = TM.C(255, 24, 12, 44), ClientSize = new Size(520, 600), TopMost = false };
        Win.Dark(f); open = f; f.FormClosed += (s, e) => { open = null; LastBrowser = null; }; f.Owner = owner;
        var wa = Screen.FromControl(owner).WorkingArea; f.Location = new Point(Math.Min(wa.Right - f.Width - 10, owner.Right - f.Width - 30), Math.Max(wa.Top + 10, owner.Top + 90));
        var wb = new WebBrowser { Dock = DockStyle.Fill, IsWebBrowserContextMenuEnabled = false, AllowWebBrowserDrop = false, ScriptErrorsSuppressed = true, ScrollBarsEnabled = false, WebBrowserShortcutsEnabled = false };
        wb.ObjectForScripting = new EmojiBridge { OnPick = e => { Used(e); try { insert(e); } catch { } } }; f.Controls.Add(wb); LastBrowser = wb;
        f.Show(owner); wb.DocumentText = BuildPage();
    }
}

// what the page of the emoji panel calls when an emoji is clicked (the browser control needs a public type that is visible to COM)
[System.Runtime.InteropServices.ComVisible(true)]
public class EmojiBridge { public Action<string> OnPick; public void Pick(string e) { if (OnPick != null) OnPick(e); } }
