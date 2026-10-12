using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

// The COLOUR MIXER on the Profile page (a box of its own, not a tab): pick two colours from every shade and every finish (gloss, metallic, metals, chrome, chameleon, candy, pearlescent,
// holographic, glitter, carbon, satin, matte, frosted glass, worn, neon glow...), a drop bar chooses the style, see the colour they make together (a gradient, five steps, a colour wheel,
// swatch pairs), and use the result as your profile colour, a theme, or the colour of your name.
sealed class MixPalette { public string Name; public int Kind; public string[] Names; public Color[] Cols, Cols2; }   // Cols2: the second colour of a chameleon (null for the others)

static class MixColours
{
    // the finishes (Kind)
    public const int Plain = 0, Metal = 1, Pearl = 2, Matte = 3, Grid = 4, Chameleon = 5, Candy = 6, Chrome = 7, Brushed = 8, Holo = 9, Glitter = 10, Carbon = 11, Satin = 12, Frost = 13, Worn = 14, Glow = 15;
    public static Color Hex(string h) { h = h.TrimStart('#'); return Color.FromArgb(255, Convert.ToInt32(h.Substring(0, 2), 16), Convert.ToInt32(h.Substring(2, 2), 16), Convert.ToInt32(h.Substring(4, 2), 16)); }
    public static string HexOf(Color c) { return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"); }
    public static Color Mix(Color a, Color b, float t) { t = Math.Max(0f, Math.Min(1f, t)); return Color.FromArgb(255, (int)Math.Round(a.R + (b.R - a.R) * t), (int)Math.Round(a.G + (b.G - a.G) * t), (int)Math.Round(a.B + (b.B - a.B) * t)); }
    // "Name#RRGGBB" or, for a chameleon, "Name#RRGGBB>RRGGBB" (the colour it shows from one side and the colour from the other)
    static MixPalette P(string name, int kind, string spec)
    {
        var names = new List<string>(); var cols = new List<Color>(); var cols2 = new List<Color>(); bool two = false;
        foreach (string it in spec.Split('|'))
        {
            int i = it.LastIndexOf('#'); if (i < 1) continue; string nm = it.Substring(0, i).Trim(), hx = it.Substring(i + 1); int gt = hx.IndexOf('>');
            if (gt > 0) { cols.Add(Hex(hx.Substring(0, gt))); cols2.Add(Hex(hx.Substring(gt + 1))); two = true; } else { cols.Add(Hex(hx)); cols2.Add(Color.Empty); }
            names.Add(nm);
        }
        return new MixPalette { Name = name, Kind = kind, Names = names.ToArray(), Cols = cols.ToArray(), Cols2 = two ? cols2.ToArray() : null };
    }
    // the grid of every shade: 36 hues (every 10 degrees) from the lightest to the darkest, then a row of greys
    public const int ShadeCols = 36, ShadeRows = 10;
    static readonly double[] ShadeL = { 0.93, 0.84, 0.74, 0.64, 0.54, 0.45, 0.36, 0.27, 0.18 };
    public static Color Shade(int col, int row) { if (row >= ShadeL.Length) { double g = 1.0 - col / (double)(ShadeCols - 1); return ThemeDef.Hsl(0, 0, g); } return ThemeDef.Hsl(col * 10, 0.85, ShadeL[row]); }
    public static string ShadeName(int col, int row) { if (row >= ShadeL.Length) return "Grey " + (int)Math.Round((1.0 - col / (double)(ShadeCols - 1)) * 100) + "%"; return "Hue " + (col * 10) + ", " + (int)Math.Round(ShadeL[row] * 100) + "% light"; }

    public static readonly List<MixPalette> All = new List<MixPalette>
    {
        new MixPalette { Name = "Every shade", Kind = Grid, Names = new string[0], Cols = new Color[0] },
        P("Classic gloss", Plain, "Black#101010|Graphite#2B2D30|Anthracite#383B3E|Steel Grey#6F7378|Silver Grey#A5A8AD|Stone Grey#8C8F8B|Pure White#F7F7F5|Frost White#E6EAEE|Bone White#E8E0CC|Ivory#F2EBD3|Cream#EFE6C8|Racing Red#D01818|Blaze Red#E8341C|Garnet Red#8E1B24|Wine Red#5C1222|Cabernet#4A1020|Sunset Red#C8322B|Salmon#F08A78|Pink#F06FA5|Hot Pink#FF3C9D|Bright Orange#FF7A00|Orange#F28C28|Gold#E3B23C|Yellow#F7D417|Race Yellow#FFE400|Dew Yellow#E8E56A|Lime Green#9BD21C|Bright Green#2EC74D|Green#1E9A48|Sea Green#1C9C7A|Olive Green#6E7C2F|Dark Green#12482C|Petrol Blue#0E5A6B|Light Blue#58B8EE|Bright Blue#1E78E6|Blue#1A4FC4|Ultra Blue#2A3FD8|Dark Blue#14246E|Midnight Blue#0B1442|Galaxy Blue#1A1F5E|Racing Blue#0C3C9E|Purple#6A2EB0|Spin Purple#8A3FD8|Chocolate#4A2C1C|Brown#6B4226|Saddle#8B5A33|Beige#CDB48E|Desert Tan#B89F73|Khaki#A59B63|Sand#D8C690|Mint#7CE0B8"),
        P("Metallic", Metal, "Metallic Black#16181B|Metallic Graphite#303438|Black Steel#4A4F55|Dark Silver#80858C|Metallic Silver#B8BCC2|Blue Silver#9AA8BC|Shadow Silver#6E7680|Stone Silver#A8A89C|Midnight Silver#3E4552|Gun Metal#3A4047|Metallic Red#C41E1E|Torch Red#E42E1A|Formula Red#D81A1A|Lava Red#B8261C|Garnet#8A1A24|Sunset#C8402B|Metallic Wine#6A1830|Candy Red Metal#A81438|Metallic Pink#E8508C|Salmon Pink#EE8A7A|Sunrise Orange#F07A1C|Metallic Orange#EE6A10|Metallic Gold#D9A92E|Bronze#A8743A|Metallic Yellow#EBC21A|Race Yellow Metal#F5D800|Metallic Lime#8CC41A|Racing Green#0E5A38|Sea Green#1A8F6E|Olive Metal#6A7A2E|Bright Green#26B84A|Gasoline Green#3E8F54|Metallic Midnight#0E1A4A|Galaxy#1C2670|Dark Blue Metal#142A82|Saxon Blue#2F58B8|Metallic Blue#1E58D8|Mariner Blue#2E6FB0|Harbor Blue#2A4E78|Diamond Blue#7FB8E8|Surf Blue#52A0D8|Nautical Blue#1D4D86|Ultra Blue Metal#2840E0|Light Blue Metal#4FB0EE|Metallic Purple#6C34C8|Spin Purple Metal#8A44E0|Racing Purple#4A2A9A|Metallic Brown#5E3A22|Copper Brown#8E5230|Champagne#D8C49A|Metallic Beige#BFA782"),
        P("Metals", Metal, "Gold#D4AF37|Antique Gold#CFB53B|Champagne Gold#F7E7CE|White Gold#EFE4B0|Yellow Gold#E6B422|Rose Gold#B76E79|Pink Gold#D79E96|Silver#C0C0C0|Sterling Silver#D3D3D6|Platinum#E5E4E2|Palladium#CED0DD|Iridium#9E9FA6|Chrome Metal#DBE4EB|Bronze#CD7F32|Dark Bronze#8C5A2B|Copper#B87333|Burnt Copper#8E4A24|Brass#B5A642|Antique Brass#8E7B3A|Steel#71797E|Stainless#B4B9BD|Titanium#878681|Gunmetal#2A3439|Iron#48494B|Pewter#8E9196|Nickel#727472|Zinc#B6B8B9|Aluminium#A9ACB0|Tin#C9CCD1|Cobalt#3D59AB|Tungsten#5A5D63|Carbon Metal#2B2B2B|Mercury#D8DADC|Lead#6C6F73|Black Chrome Metal#3A3B3F|Blued Steel#33455E|Damascus#6A6E73|Rust Iron#8A4B2D|Verdigris#4C9A86|Patina#5B8C7A"),
        P("Brushed metals", Brushed, "Brushed Steel#A9ADB2|Brushed Black Steel#3C3F44|Brushed Aluminium#B9BCC0|Brushed Gold#CDA83C|Pure Gold#E6B422|Brushed Rose Gold#C9857F|Brushed Copper#B87333|Brushed Bronze#A8743A|Brushed Titanium#85847F|Brushed Nickel#8E908E|Brushed Platinum#DAD9D5|Brushed Brass#B5A642|Brushed Silver#C4C6CA|Brushed Gunmetal#454C52|Brushed Chrome#CFD6DC|Brushed Pewter#8C8F95|Brushed Zinc#A9ABAB|Brushed Cobalt#4A68B0|Brushed Black Gold#4A3C1C|Brushed Graphite#4A4D52"),
        P("Chrome and mirror", Chrome, "Chrome#D8DEE6|Black Chrome#34363C|Gold Chrome#E0B83A|Rose Chrome#D08A92|Blue Chrome#4A74D0|Red Chrome#D03028|Green Chrome#2CAA5C|Purple Chrome#8A4AD0|Copper Chrome#C8803C|Silver Mirror#E8ECF0|Liquid Metal#B5BBC4|Mercury Pool#C6CCD2|Oil Slick Chrome#4A4A6A|Teal Chrome#2AA8A0|Pink Chrome#E868B0|Orange Chrome#F08A28|Gunmetal Chrome#5A6068|Platinum Chrome#E6E8EA"),
        P("Chameleon (colour shifting)", Chameleon, "Cosmic (purple to green)#7A2EC8>1FB868|Twilight (blue to purple)#2A4FD8>9A3EC8|Lagoon (green to blue)#1FB868>2A6FD8|Ember (red to orange)#D81E1E>F08A1C|Sunbeam (orange to gold)#F07A1C>E8C23A|Candy Floss (pink to blue)#F06FA5>4F8AE8|Neon Tide (magenta to cyan)#E01FD0>1FD8E8|Deep Sea (teal to purple)#1FA8A0>7A3EC8|Meadow (gold to green)#E0B83A>2CAA5C|Dusk (bronze to purple)#A8743A>7A3EC8|Glacier (blue to teal)#2A6FD8>1FB8B0|Orchid (violet to pink)#8A3EC8>F06FA5|Verdigris (copper to green)#B87333>2CAA8C|Midnight Purple (black to purple)#101018>7A2EC8|Midnight Blue (black to blue)#101018>2A4FD8|Midnight Green (black to green)#101018>1FB868|Frost (silver to blue)#B8BCC2>4F8AE8|Blush (pearl to pink)#F2EEF2>F06FA5|Wine (red to purple)#C81E3A>7A2EC8|Mirage (cyan to magenta)#1FD8E8>E01FD0|Citrus (lime to gold)#9BD21C>E8C23A|Flamingo (orange to pink)#F08A1C>F06FA5|Gem (emerald to sapphire)#1FA860>2A4FD8|Royal (ruby to amethyst)#C81E3A>8A3EC8|Sunset (orange to magenta)#F07A1C>D81FA0|Ocean (teal to navy)#1FA8B0>1A2A78|Aurora (green to violet)#2CD87A>8A3EC8|Peacock (blue to green)#1E6FE0>1FB878|Beetle (green to gold)#1FA85A>D8B83A|Oil Slick (purple to teal)#7A2EC8>1FB8B0|Nebula (violet to blue)#8A3EC8>2A5FE0|Fire Opal (aqua to pink)#4FD8D0>F06FA5|Dragonfly (teal to lime)#1FB8B0>9BD21C|Galaxy (navy to violet)#142A78>8A3EC8|Rose Gold Shift (rose to green)#D08A92>2CAA8C|Copper Flame (copper to red)#B87333>D81E1E|Sapphire Fire (blue to red)#2A4FD8>D81E1E|Amber Jade (amber to green)#E0A81C>1FA868|Ice Fire (cyan to orange)#1FD8E8>F07A1C|Void (black to teal)#0C0C14>1FB8A8"),
        P("Candy", Candy, "Candy Red#B8102A|Candy Apple#C8142C|Candy Orange#E8601C|Candy Gold#D89A1C|Candy Yellow#F0C81C|Candy Lime#6EC41C|Candy Green#16A04A|Candy Teal#14A8A0|Candy Blue#1C5FD8|Candy Cobalt#1C3FB8|Candy Purple#7A2AC0|Candy Violet#8E3AD8|Candy Pink#E8409A|Candy Magenta#C81C8E|Candy Rose#E0507A|Candy Black#16101C|Candy Burgundy#6A1230|Candy Cyan#14B8E0|Candy Plum#5A1C5E|Candy Tangerine#F08A2C"),
        P("Pearlescent", Pearl, "Pearl White#F8F6F0|Opal#A8C3BC|Moonstone#D9E2E6|Abalone#7FB5B5|Mother of Pearl#EAE0C8|Iridescent Pink#F4C2E7|Iridescent Blue#A9D6F5|Iridescent Lilac#D1B3F2|Aurora Green#A7F0D1|Peach Pearl#FFD9C2|Champagne Pearl#F2DFC4|Pearl Black#2E2A33|Sea Pearl#9DD6D0|Rose Pearl#F2C9D3|Violet Pearl#B8A9E0|Gold Pearl#F0D98C|Silver Pearl#D6D9E0|Midnight Pearl#1F2748|Candy Red Pearl#E0415B|Candy Blue Pearl#3D7BE8|Ice Pearl#DCEFF7|Sunset Pearl#F6B28B|Mint Pearl#BFEBD8|Plum Pearl#8E5C9B|Tri-coat White#F4F1EA|Pearl Red#C8283C|Pearl Green#2FA878|Pearl Orange#F09A52|Pearl Grey#B7BCC4"),
        P("Holographic and iridescent", Holo, "Holographic Silver#CFD4DC|Holographic Gold#E6C86A|Holographic Pink#F2A8D8|Holographic Blue#8FC8F2|Holographic Green#9FE8C8|Holographic Purple#B89AE8|Rainbow Foil#C8B8E8|Oil on Water#6A78B8|Prism#E0E8F8|Unicorn#F0B8E8|Opal Fire#E8B8C8|Aurora Foil#8FE8D8|Soap Bubble#CFE0F2|Dichroic Glass#B898E0"),
        P("Glitter and sparkle", Glitter, "Gold Glitter#D8B040|Silver Glitter#BFC3CA|Rose Glitter#D08A92|Blue Glitter#3A6FD8|Pink Glitter#F060A8|Purple Glitter#8A3EC8|Green Glitter#2CAA5C|Red Glitter#C81E2A|Black Glitter#1C1C20|White Glitter#F4F4F4|Copper Glitter#B87333|Rainbow Sparkle#C8A8E8|Stardust#7A68B8|Diamond Dust#E0E8F0|Champagne Sparkle#EAD8B0|Midnight Sparkle#1A2048|Teal Glitter#1FA8A0|Orange Glitter#F08A28"),
        P("Carbon and tech", Carbon, "Carbon Black#26272B|Carbon Blue#1F3F7E|Carbon Red#8E1E24|Carbon Silver#6E727A|Carbon Gold#8E7430|Carbon Green#1F5E3E|Kevlar Yellow#C8A41C|Graphite Weave#3C3F44|Carbon Purple#4A2A7E|Carbon Orange#A8541C|Carbon Teal#1F6E6E|Carbon Pink#8E2E5E|Carbon White#B8BCC0|Carbon Bronze#7E5A30"),
        P("Satin and soft sheen", Satin, "Satin Black#202226|Satin White#EDEDEA|Satin Grey#8E9196|Satin Silver#B4B7BC|Satin Gold#C8A84E|Satin Red#B8242C|Satin Blue#2A4FA8|Satin Green#2A7E4E|Satin Purple#6A3AA0|Satin Orange#D8741C|Satin Pink#D878A0|Satin Teal#2A8E90|Satin Bronze#9A6E3C|Satin Champagne#D8C8A8|Satin Navy#1C2A4E|Satin Burgundy#6A1E34"),
        P("Matte", Matte, "Matte Black#1C1C1E|Charcoal#36393F|Graphite#3B3C3E|Slate#5B6670|Stone#8A8D8F|Sand#CBB994|Sage#8DA290|Olive#6B7048|Terracotta#C0674A|Clay#B07A5F|Dusty Rose#C9908F|Mauve#A98AA3|Matte Navy#1F2A44|Matte Forest#2F4A3A|Matte Burgundy#5E1F2B|Matte Mustard#C49A2C|Matte Teal#2F6F73|Cream#EFE7D6|Bone#E3DAC9|Cocoa#5A3E36|Matte White#EDEDED|Matte Grey#6E7074|Matte Blue#3E5C76|Matte Green#4F6F52|Matte Red#8C2F39|Matte Orange#C8702A|Matte Plum#5B3A5E|Putty#B8AFA3|Matte Pink#C8808E|Matte Yellow#C8B040|Matte Purple#5E4A7E|Matte Brown#5A4234"),
        P("Frosted glass", Frost, "Clear Ice#DFF2F8|Smoked Glass#4A4E58|Blue Ice#8FD0F0|Rose Glass#F0A8C0|Green Glass#7FD8A8|Amber Glass#E8B050|Purple Glass#A888E0|Black Glass#222630|Frost White#F2F6F8|Sea Glass#9FD8CC|Cyan Glass#5FD8F0|Lilac Glass#C8B0F0"),
        P("Worn and weathered", Worn, "Worn Black#2A2A2C|Worn Graphite#4A4C50|Worn Silver#8E9094|Worn White#D0CCC0|Worn Red#8E2E28|Worn Orange#B8641E|Worn Yellow#B8A030|Worn Green#4E6E3E|Worn Blue#3E5A82|Worn Brown#6A4A30|Worn Sand#B8A47E|Worn Olive#6A6A3E|Worn Rust#8E4A28|Worn Teal#3E7A78|Worn Pink#B87A82|Worn Purple#5E4A78"),
        P("Neon and glow", Glow, "Neon Pink#FF2BD6|Neon Cyan#00E5FF|Neon Green#39FF14|Neon Yellow#F5FF00|Neon Orange#FF6700|Neon Purple#BC13FE|Neon Blue#1F51FF|Neon Red#FF073A|Laser Lime#BFFF00|Electric Violet#8F00FF|Hot Magenta#FF1DCE|Plasma#E100FF|Ultra Blue#3D5AFE|Acid Green#B0FF00|Radioactive#CCFF00|Cyber Teal#00FFC6|Flamingo#FF6EC7|Solar#FFD400|Ice#B9F2FF|Lightning#FFF200"),
        P("Basic colours", Plain, "Red#E53935|Crimson#DC143C|Scarlet#FF2400|Coral#FF6B6B|Orange#FB8C00|Amber#FFC107|Yellow#FFEB3B|Lime#A4DE02|Green#43A047|Emerald#50C878|Mint#98FF98|Teal#009688|Turquoise#4ECDC4|Cyan#00BCD4|Sky Blue#4FC3F7|Blue#1E88E5|Royal Blue#4169E1|Navy#1A237E|Indigo#3F51B5|Violet#8E24AA|Purple#6A1B9A|Magenta#FF00FF|Pink#EC407A|Hot Pink#FF69B4|Rose#FF007F|Brown#6D4C41|Black#111111|Grey#9E9E9E|White#FAFAFA"),
        P("Pastels", Plain, "Baby Pink#F8BBD0|Baby Blue#BBDEFB|Mint Cream#C8F7DC|Lavender#D1C4E9|Peach#FFCCBC|Lemon#FFF9C4|Lilac#E1BEE7|Powder Blue#B3E5FC|Sage Mist#C5E1A5|Blush#FFCDD2|Apricot#FFE0B2|Seafoam#B2DFDB|Periwinkle#C5CAE9|Cotton Candy#FFBCF2|Buttercream#FFF3C4|Sky Mist#D6F1FF|Rose Quartz#F7CAC9|Serenity#92A8D1|Pistachio#C1E1C1|Orchid Mist#EBD3F8"),
        P("Jewel tones", Plain, "Ruby#9B111E|Emerald#046307|Sapphire#0F52BA|Amethyst#9966CC|Topaz#FFC87C|Garnet Stone#733635|Jade#00A86B|Onyx#353839|Opal Blue#2B5F75|Peridot#E6E200|Aquamarine#7FFFD4|Citrus Stone#E4D00A|Tanzanite#4A5FA5|Rhodolite#B03060|Turquoise Stone#30D5C8|Lapis Lazuli#26619C|Tourmaline#B8336A|Moonstone Blue#7A9BB5"),
        P("Earth and nature", Plain, "Moss#8A9A5B|Fern#4F7942|Pine#01796F|Bark#5C4033|Mud#70543E|Rust#B7410E|Ochre#CC7722|Sandstone#C2B280|Dune#E0C9A6|Clay Red#A5522E|Slate Rock#708090|River Stone#8B8C89|Lichen#A9BA9D|Heather#B7A6C3|Wheat#F5DEB3|Autumn#C0562F|Forest#228B22|Deep Moss#3B4A2F|Oak#B08850|Walnut#5C4328|Cherry Wood#8A3E2C|Birch#E6D8BE"),
        P("Sky and sea", Plain, "Sky#87CEEB|Deep Sky#00BFFF|Ocean#006994|Deep Sea#003B5C|Lagoon#1FB5B5|Surf#7ADBE3|Cloud#E8EEF2|Storm#4B5A66|Dusk#5B4B8A|Twilight Sky#3E3768|Midnight#101A3A|Dawn#F2B6A0|Tide#5BA3B0|Glacier Ice#CFEFF5"),
        P("Fire and sunset", Plain, "Flame#FF4500|Ember#E25822|Lava#CF1020|Sunset Orange#FF7A3D|Sunset Pink#FF8FA3|Sunset Purple#9B5DE5|Golden Hour#FFB347|Molten#FF6A00|Magma#B22222|Ash#4A4A4A|Cinder#7A3E2B|Blaze#FF5E00|Spark#FFD166|Phoenix#E8590C"),
        P("Flowers and plants", Plain, "Rose Red#C8102E|Tulip Pink#F28CB1|Lily White#F6F2E8|Sunflower#F4B400|Lavender Bloom#9C7CCB|Violet Flower#7E4FB5|Orchid#DA70D6|Peony#F4A6B8|Poppy#E03C31|Daisy#F6E27F|Bluebell#5B6FD0|Marigold#F29C1F|Hydrangea#7FA6E0|Camellia#E7798F|Fuchsia#D0218A|Jasmine#F8F0C8|Lotus#EBA8C8|Iris#4B3FA0|Leaf Green#4E9A3A|Fern Frond#3E7E3A|Basil#2E6A32|Mint Leaf#7CC9A0|Cactus#5E8A5A|Bamboo#9AAE5C"),
        P("Wood and stone", Plain, "Oak Wood#B08850|Walnut Wood#5C4328|Cherry Wood#8A3E2C|Birch Wood#E6D8BE|Pine Wood#D2A86A|Mahogany#4E1E12|Ebony#2A2018|Teak#A8743A|Maple#D8B07A|Cedar#A8643C|Marble White#ECE9E4|Marble Grey#B8B8B6|Granite#6E6E70|Slate Stone#4E555C|Limestone#D9D2BE|Sandstone Wall#C9A57A|Basalt#363A3E|Brick Red#A04030|Concrete#9A9A98|Terracotta Tile#C87050"),
        P("Vintage and retro", Plain, "Retro Teal#2A9D8F|Retro Orange#E76F51|Mustard Yellow#E9C46A|Avocado#6B8E23|Harvest Gold#DA9100|Burnt Orange#CC5500|Dusty Blue#6C8EAD|Faded Denim#5B7A9A|Sepia#704214|Old Rose#C08081|Polaroid Cream#F1E5C8|Diner Red#B22222|Jukebox Blue#3B6BA5|Seafoam Retro#7FCDBB|Poodle Pink#F4A7B9|Neon Sign Pink#FF4D9E|Arcade Purple#6B3FA0|Cassette Grey#7A7A78"),
        P("Military and utility", Plain, "Olive Drab#6B6B3A|Army Green#4B5320|Camo Green#78866B|Desert Sand#C2B280|Coyote Brown#81613C|Navy Grey#4E5560|Gunship Grey#5C6064|Khaki Utility#B5A66A|Black Ops#1B1D20|Hazard Yellow#F2C500|Safety Orange#FF6700|Utility Blue#2F4A6E|Tactical Tan#B09870|Smoke Green#5E6B58"),
        P("Skin and hair", Plain, "Porcelain#F4E3D7|Fair#EBCDB5|Light#E0B896|Warm Beige#D4A57C|Golden#C68E5E|Tan#A9744B|Caramel#8E5A38|Brown Skin#6E4128|Deep Brown#4E2E1C|Espresso#2E1A10|Blonde#E8D08A|Honey Blonde#C9A055|Auburn#8E3B1F|Chestnut#6A3A22|Raven#16141A|Silver Hair#C8CCD0"),
        P("Sweets and drinks", Plain, "Strawberry#E2425C|Raspberry#B8174A|Blueberry#4F5FB5|Blackcurrant#3A1F4E|Grape#6F2DA8|Lemonade#F7E56A|Orange Juice#F8A23A|Lime Soda#9ED84A|Mint Choc#7FD8B0|Chocolate Bar#5A3220|Caramel Swirl#C98B3C|Vanilla#F4E8C8|Bubblegum Pink#FF9ECF|Cotton Candy Blue#9ED8F8|Cherry Cola#5E1620|Coffee#6F4E37|Latte#C8A27E|Matcha#8AA74E"),
        P("Seasons", Plain, "Spring Green#8ED08A|Spring Blossom#F2B8CF|Spring Sky#9DD4F2|Summer Sun#FFC93C|Summer Sea#2CB1BC|Summer Coral#FF7F6E|Autumn Leaf#C8561E|Autumn Gold#D89A2C|Autumn Plum#6E2E4E|Winter Frost#CFE6F2|Winter Pine#27493E|Winter Berry#9E1B32|Winter Night#1A2442|Monsoon#3E5A6E|Harvest Moon#E8B04A|Snowdrift#F0F4F8"),
        P("Greys and blacks", Plain, "Pure White#FFFFFF|Snow#F5F5F5|Mist#E0E0E0|Fog#CFCFCF|Silver Grey#BDBDBD|Ash Grey#9E9E9E|Smoke#757575|Steel Grey#616161|Charcoal Grey#424242|Coal#2D2D2D|Ink#1A1A1A|Jet#0D0D0D|Pure Black#000000|Blue Grey#607D8B")
    };
    // the pairs of the reference sheet
    public static readonly string[][] Pairs = { new[] { "Orange + Blue", "#FF9F43", "#0ABDE3" }, new[] { "Yellow + Purple", "#FFE600", "#7B2D9E" }, new[] { "Green + Red", "#43A047", "#E53935" } };
    public static double Luma(Color c) { return (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0; }
    public static int IndexOf(string name) { for (int i = 0; i < All.Count; i++) if (All[i].Name == name) return i; return -1; }
    public static int Total { get { int t = ShadeCols * ShadeRows; foreach (var p in All) t += p.Cols.Length; return t; } }
}

sealed class MixPreset { public string Name = ""; public Color A, A2, B, B2; public int KA, KB; public string NA = "", NB = ""; public float T = 0.5f; }

// what the colour mixer remembers between runs: where it was left (the two colours, the mix, the two styles, the two switches) and the saved colour presets
static class MixStore
{
    public static readonly List<MixPreset> Presets = new List<MixPreset>();
    public static MixPreset State = new MixPreset { A = Color.FromArgb(255, 255, 107, 107), B = Color.FromArgb(255, 78, 205, 196), NA = "Coral", NB = "Turquoise" };
    public static int CatA = -1, CatB = -1; public static bool Follow = true, Fade = true, Loaded;
    static string FileP { get { return Path.Combine(StudioEnv.DataDir, "colour-mix.txt"); } }
    static string C(Color c) { return c.IsEmpty ? "-" : MixColours.HexOf(c).Substring(1); }
    static Color P(string s) { try { return s == "-" || s.Length != 6 ? Color.Empty : MixColours.Hex(s); } catch { return Color.Empty; } }
    static string E(string s) { return Uri.EscapeDataString(s ?? ""); }
    static string U(string s) { try { return Uri.UnescapeDataString(s ?? ""); } catch { return s ?? ""; } }
    static string Line(string key, MixPreset p, params object[] extra) { var sb = new StringBuilder(key + "=" + E(p.Name) + "|" + C(p.A) + "|" + C(p.A2) + "|" + p.KA + "|" + E(p.NA) + "|" + C(p.B) + "|" + C(p.B2) + "|" + p.KB + "|" + E(p.NB) + "|" + p.T.ToString("0.###", CultureInfo.InvariantCulture)); foreach (var x in extra) sb.Append('|').Append(x); return sb.ToString(); }
    static MixPreset Parse(string v, out string[] rest)
    {
        rest = new string[0]; var a = v.Split('|'); if (a.Length < 10) return null; float t; if (!float.TryParse(a[9], NumberStyles.Float, CultureInfo.InvariantCulture, out t)) t = 0.5f; int ka, kb; int.TryParse(a[3], out ka); int.TryParse(a[7], out kb);
        var ca = P(a[1]); var cb = P(a[5]); if (ca.IsEmpty || cb.IsEmpty) return null; rest = new string[a.Length - 10]; Array.Copy(a, 10, rest, 0, rest.Length);
        return new MixPreset { Name = U(a[0]), A = ca, A2 = P(a[2]), KA = ka, NA = U(a[4]), B = cb, B2 = P(a[6]), KB = kb, NB = U(a[8]), T = Math.Max(0f, Math.Min(1f, t)) };
    }
    public static void Load()
    {
        Loaded = true; Presets.Clear();
        try
        {
            if (!File.Exists(FileP)) return;
            foreach (string l in File.ReadAllLines(FileP, Encoding.UTF8))
            {
                int i = l.IndexOf('='); if (i < 1) continue; string k = l.Substring(0, i), v = l.Substring(i + 1); string[] rest; var p = Parse(v, out rest); if (p == null) continue;
                if (k == "state") { State = p; int n; if (rest.Length > 0 && int.TryParse(rest[0], out n)) CatA = n; if (rest.Length > 1 && int.TryParse(rest[1], out n)) CatB = n; if (rest.Length > 2) Follow = rest[2] != "0"; if (rest.Length > 3) Fade = rest[3] != "0"; }
                else if (k == "preset" && Presets.Count < 80) Presets.Add(p);
            }
        }
        catch { }
    }
    public static void Save()
    {
        try { Directory.CreateDirectory(StudioEnv.DataDir); var sb = new StringBuilder(); sb.Append(Line("state", State, CatA, CatB, Follow ? "1" : "0", Fade ? "1" : "0")).Append('\n'); foreach (var p in Presets) sb.Append(Line("preset", p)).Append('\n'); Win.WriteAtomic(FileP, sb.ToString(), new UTF8Encoding(false)); } catch { }
    }
}

partial class StudioForm
{
    Color mixA = Color.FromArgb(255, 255, 107, 107), mixB = Color.FromArgb(255, 78, 205, 196), mixA2 = Color.Empty, mixB2 = Color.Empty; int mixKA = 0, mixKB = 0, mixCatA = 1, mixCatB = 1, mixPresetSel = -1; float mixT = 0.5f, mixH = 1500f, mixWheelRot; string mixNameA = "Coral", mixNameB = "Turquoise", mixHoverA = "", mixHoverB = "", mixStatus = "";
    bool mixFollow = true, mixFade = true, mixLoaded; DateTime mixDirty = DateTime.MinValue;
    public Color MixedNow { get { return MixColours.Mix(mixA, mixB, mixT); } }
    // the merged colour wears the finish of A and B when they are the same (two chameleons make a chameleon); otherwise it is plain
    int MixedKind { get { return mixKA == mixKB ? mixKA : 0; } }
    Color MixedSecond { get { if (mixKA != mixKB) return Color.Empty; Color a2 = mixA2.IsEmpty ? mixA : mixA2, b2 = mixB2.IsEmpty ? mixB : mixB2; return mixKA == MixColours.Chameleon ? MixColours.Mix(a2, b2, mixT) : Color.Empty; } }

    void MixEnsure()
    {
        if (mixLoaded) return; mixLoaded = true; MixStore.Load(); var s = MixStore.State; mixA = s.A; mixA2 = s.A2; mixKA = s.KA; mixNameA = s.NA; mixB = s.B; mixB2 = s.B2; mixKB = s.KB; mixNameB = s.NB; mixT = s.T;
        int def = MixColours.All.FindIndex(p => p.Name == "Classic gloss"); if (def < 1) def = 1; mixCatA = MixStore.CatA >= 0 && MixStore.CatA < MixColours.All.Count ? MixStore.CatA : def; mixCatB = MixStore.CatB >= 0 && MixStore.CatB < MixColours.All.Count ? MixStore.CatB : def; mixFollow = MixStore.Follow; mixFade = MixStore.Fade;
    }
    // a colour that can be read on the dark pages (the name is written in it)
    static Color MixLegible(Color c) { for (int i = 0; i < 8 && MixColours.Luma(c) < 0.38; i++) c = MixColours.Mix(c, Color.White, 0.18f); return c; }
    // the profile follows the mix: whatever you change here IS your profile colour (the banner, the rings, the accents), and your name fades from A to B (or takes the merged colour)
    internal void MixApplyProfile()
    {
        if (!mixFollow) return; ProfileStore.Accent = ProfileStore.Pack(MixedNow); palSeen = ProfileStore.Accent;
        if (mixFade) { ProfileStore.NameRgb = ProfileStore.Pack(MixLegible(mixA)); ProfileStore.NameRgb2 = ProfileStore.Pack(MixLegible(mixB)); } else { ProfileStore.NameRgb = ProfileStore.Pack(MixLegible(MixedNow)); ProfileStore.NameRgb2 = -1; }
    }
    internal void MixChanged() { mixDirty = DateTime.Now; MixApplyProfile(); }
    internal void MixFlush()
    {   // saved and sent a moment after the last change (not on every step of a slider)
        mixDirty = DateTime.MinValue; var s = MixStore.State; s.A = mixA; s.A2 = mixA2; s.KA = mixKA; s.NA = mixNameA; s.B = mixB; s.B2 = mixB2; s.KB = mixKB; s.NB = mixNameB; s.T = mixT; MixStore.CatA = mixCatA; MixStore.CatB = mixCatB; MixStore.Follow = mixFollow; MixStore.Fade = mixFade; MixStore.Save();
        if (mixFollow) { ProfileStore.Save(); QueueRepublish(); }
    }
    internal void MixSet(int side, Color c, int kind, string name, Color c2 = default(Color)) { if (side == 0) { mixA = c; mixKA = kind; mixNameA = name; mixA2 = c2; } else { mixB = c; mixKB = kind; mixNameB = name; mixB2 = c2; } mixStatus = ""; mixPresetSel = -1; MixChanged(); }
    internal void MixSetFollow(bool on)
    {
        mixFollow = on; if (on) MixApplyProfile(); else { ProfileStore.Accent = -1; palSeen = ProfileStore.Accent; ProfileStore.NameRgb = -1; ProfileStore.NameRgb2 = -1; }
        mixStatus = on ? "Your profile colour is your mix: " + MixColours.HexOf(MixedNow) + "." : "Your profile is back to the colour of your theme."; mixDirty = DateTime.Now; if (on) MixApplyProfile(); MixFlush(); Toast(mixStatus);
    }
    internal void MixSetFade(bool on) { mixFade = on; MixApplyProfile(); mixStatus = on ? "Your name fades from colour A to colour B." : "Your name takes the merged colour."; MixFlush(); Toast(mixStatus); }
    internal void MixSwap() { var tc = mixA; var tc2 = mixA2; int tk = mixKA; string tn = mixNameA; mixA = mixB; mixA2 = mixB2; mixKA = mixKB; mixNameA = mixNameB; mixB = tc; mixB2 = tc2; mixKB = tk; mixNameB = tn; mixPresetSel = -1; MixChanged(); }
    internal void MixSetT(float t) { mixT = (float)Math.Round(Math.Max(0f, Math.Min(1f, t)) * 100) / 100f; mixPresetSel = -1; MixChanged(); }

    // ---- saved colour presets: the two colours, their finishes and the mix, so a merged colour can be brought back later ----
    internal MixPreset MixPresetSave()
    {
        string nm = mixNameA + " + " + mixNameB; string baseName = nm; int n = 2; while (MixStore.Presets.Exists(p => string.Equals(p.Name, nm, StringComparison.OrdinalIgnoreCase) && !(p.A == mixA && p.B == mixB && Math.Abs(p.T - mixT) < 0.005f))) nm = baseName + " " + n++;
        var same = MixStore.Presets.Find(p => p.A == mixA && p.B == mixB && p.A2 == mixA2 && p.B2 == mixB2 && p.KA == mixKA && p.KB == mixKB && Math.Abs(p.T - mixT) < 0.005f); if (same != null) { mixPresetSel = MixStore.Presets.IndexOf(same); mixStatus = "That mix is already saved as \"" + same.Name + "\"."; Toast(mixStatus); return same; }
        var pr = new MixPreset { Name = nm, A = mixA, A2 = mixA2, KA = mixKA, NA = mixNameA, B = mixB, B2 = mixB2, KB = mixKB, NB = mixNameB, T = mixT }; MixStore.Presets.Add(pr); if (MixStore.Presets.Count > 80) MixStore.Presets.RemoveAt(0); mixPresetSel = MixStore.Presets.IndexOf(pr); MixFlush();
        mixStatus = "Saved the colour preset \"" + pr.Name + "\" (" + MixStore.Presets.Count + " saved)."; Toast(mixStatus); return pr;
    }
    internal void MixPresetLoad(int i)
    {
        if (i < 0 || i >= MixStore.Presets.Count) return; var p = MixStore.Presets[i]; mixA = p.A; mixA2 = p.A2; mixKA = p.KA; mixNameA = p.NA; mixB = p.B; mixB2 = p.B2; mixKB = p.KB; mixNameB = p.NB; mixT = p.T; mixPresetSel = i; MixChanged(); MixFlush(); mixStatus = "Colour preset " + (i + 1) + " of " + MixStore.Presets.Count + ": " + p.Name + " (" + MixColours.HexOf(MixedNow) + ")."; }
    internal void MixPresetStep(int d) { int n = MixStore.Presets.Count; if (n == 0) { mixStatus = "No colour presets saved yet: press SAVE COLOUR PRESET."; return; } int i = mixPresetSel < 0 ? (d > 0 ? 0 : n - 1) : (mixPresetSel + d + n) % n; MixPresetLoad(i); }
    internal void MixPresetDelete()
    {
        if (mixPresetSel < 0 || mixPresetSel >= MixStore.Presets.Count) { mixStatus = "Pick a saved preset first (click one, or use PREV and NEXT), then DELETE."; return; }
        string nm = MixStore.Presets[mixPresetSel].Name; MixStore.Presets.RemoveAt(mixPresetSel); mixPresetSel = -1; MixFlush(); mixStatus = "Deleted the colour preset \"" + nm + "\"."; Toast(mixStatus);
    }

    static int MixHash(int a, int b) { unchecked { int h = a * 73856093 ^ b * 19349663; h ^= h >> 13; h *= 1274126177; return h ^ (h >> 16); } }
    // one swatch in the finish of its family: plain gloss, metallic, chrome (a mirror with a horizon), chameleon (two colours that shift), candy, brushed, pearlescent, holographic, glitter, carbon, satin, matte, frosted glass, worn, neon glow
    void MixSwatch(Graphics g, RectangleF r, Color c, int kind, float rad, Color c2 = default(Color))
    {
        if (r.Width < 2 || r.Height < 2) return;
        Func<Color, float, Color> sh = (col, t) => t >= 0 ? MixC(col, Color.White, t) : MixC(col, Color.Black, -t);
        if (kind == MixColours.Glow) { for (int i = 3; i >= 1; i--) using (var p0 = RR(RectangleF.Inflate(r, i * 2.2f, i * 2.2f), rad + i * 2)) using (var br = new SolidBrush(Color.FromArgb(34 - i * 7, c))) g.FillPath(br, p0); }
        int seed = MixHash((int)Math.Round(r.X * 7), (int)Math.Round(r.Y * 13)) ^ c.ToArgb();
        using (var p = RR(r, rad))
        {
            using (var br = new SolidBrush(c)) g.FillPath(br, p);
            var gs = g.Save(); g.SetClip(p, CombineMode.Intersect);
            switch (kind)
            {
                case 1:   // metallic / metal: bands of light and dark across the surface
                    { var cb = new ColorBlend(6) { Colors = new[] { sh(c, 0.55f), sh(c, 0.10f), sh(c, -0.30f), sh(c, 0.30f), sh(c, -0.12f), sh(c, 0.22f) }, Positions = new[] { 0f, 0.22f, 0.45f, 0.62f, 0.82f, 1f } }; using (var br = new LinearGradientBrush(r, c, c, 35f)) { br.InterpolationColors = cb; g.FillPath(br, p); } }
                    break;
                case 2:   // pearlescent: a pale base with a soft sheen of pink, blue and mint moving across it
                    { var cb = new ColorBlend(5) { Colors = new[] { Color.FromArgb(120, 255, 190, 240), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(110, 170, 220, 255), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(120, 190, 255, 220) }, Positions = new[] { 0f, 0.28f, 0.5f, 0.72f, 1f } };
                      using (var br = new LinearGradientBrush(r, c, c, 40f)) { br.InterpolationColors = cb; g.FillPath(br, p); } using (var br = new LinearGradientBrush(r, Color.FromArgb(120, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillPath(br, p); }
                    break;
                case 3:   // matte: flat and soft, no shine
                    using (var br = new LinearGradientBrush(r, Color.FromArgb(0, 0, 0, 0), Color.FromArgb(46, 0, 0, 0), 90f)) g.FillPath(br, p);
                    break;
                case 5:   // chameleon: one colour on one side, the other on the other, and a glossy top
                    { Color s2 = c2.IsEmpty ? ThemeDef.Hsl((c.GetHue() + 130) % 360, 0.8, 0.5) : c2; var cb = new ColorBlend(5) { Colors = new[] { c, sh(MixC(c, s2, 0.35f), 0.10f), sh(MixC(c, s2, 0.5f), -0.05f), sh(MixC(c, s2, 0.8f), 0.08f), s2 }, Positions = new[] { 0f, 0.28f, 0.5f, 0.75f, 1f } };
                      using (var br = new LinearGradientBrush(r, c, s2, 35f)) { br.InterpolationColors = cb; g.FillPath(br, p); } var top = new RectangleF(r.X, r.Y, r.Width, r.Height * 0.5f); using (var br = new LinearGradientBrush(top, Color.FromArgb(110, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillRectangle(br, top); }
                    break;
                case 6:   // candy: a deep, wet colour with a bright highlight
                    using (var br = new LinearGradientBrush(r, sh(c, 0.22f), sh(c, -0.42f), 90f)) g.FillPath(br, p);
                    using (var br = new SolidBrush(Color.FromArgb(120, 255, 255, 255))) g.FillEllipse(br, r.X + r.Width * 0.12f, r.Y + r.Height * 0.07f, r.Width * 0.55f, r.Height * 0.3f);
                    using (var pen = new Pen(Color.FromArgb(60, 0, 0, 0), 3f)) g.DrawPath(pen, p);
                    break;
                case 7:   // chrome: a mirror: sky above, a dark horizon, ground below
                    { var cb = new ColorBlend(7) { Colors = new[] { MixC(Color.White, c, 0.10f), MixC(Color.FromArgb(255, 150, 190, 230), c, 0.25f), MixC(Color.FromArgb(255, 40, 50, 70), c, 0.20f), Color.White, MixC(Color.FromArgb(255, 140, 120, 90), c, 0.35f), sh(c, -0.35f), sh(c, -0.55f) }, Positions = new[] { 0f, 0.34f, 0.48f, 0.5f, 0.62f, 0.85f, 1f } };
                      using (var br = new LinearGradientBrush(r, c, c, 90f)) { br.InterpolationColors = cb; g.FillPath(br, p); } }
                    break;
                case 8:   // brushed metal: fine lines along the metal
                    using (var br = new LinearGradientBrush(r, sh(c, 0.20f), sh(c, -0.14f), 0f)) g.FillPath(br, p);
                    for (float y = r.Y + 1; y < r.Bottom; y += 2f) { int hv = MixHash((int)(y * 3), seed); using (var pen = new Pen(Color.FromArgb(10 + (hv & 15), (hv & 16) != 0 ? 255 : 0, (hv & 16) != 0 ? 255 : 0, (hv & 16) != 0 ? 255 : 0), 1f)) g.DrawLine(pen, r.X, y, r.Right, y); }
                    break;
                case 9:   // holographic: a rainbow that slides across a pale base
                    { var cb = new ColorBlend(7) { Colors = new[] { Color.FromArgb(170, 255, 120, 200), Color.FromArgb(170, 255, 220, 120), Color.FromArgb(170, 140, 255, 160), Color.FromArgb(170, 120, 220, 255), Color.FromArgb(170, 170, 140, 255), Color.FromArgb(170, 255, 140, 220), Color.FromArgb(170, 255, 230, 140) }, Positions = new[] { 0f, 0.17f, 0.33f, 0.5f, 0.67f, 0.83f, 1f } };
                      using (var br = new LinearGradientBrush(r, c, c, 30f)) { br.InterpolationColors = cb; g.FillPath(br, p); } using (var br = new LinearGradientBrush(r, Color.FromArgb(120, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillPath(br, p); }
                    break;
                case 10:  // glitter: a rich base with little sparks
                    using (var br = new LinearGradientBrush(r, sh(c, 0.15f), sh(c, -0.2f), 60f)) g.FillPath(br, p);
                    for (int i = 0; i < 26; i++) { int hv = MixHash(i, seed); float px = r.X + (hv & 0xFF) / 255f * r.Width, py = r.Y + ((hv >> 8) & 0xFF) / 255f * r.Height, sz = 1.2f + ((hv >> 16) & 3) * 0.7f; using (var br = new SolidBrush(Color.FromArgb(150 + ((hv >> 20) & 63), 255, (hv & 0x400) != 0 ? 255 : 240, (hv & 0x400) != 0 ? 255 : 170))) g.FillEllipse(br, px, py, sz, sz); }
                    break;
                case 11:  // carbon fibre: a woven pattern
                    using (var br = new SolidBrush(sh(c, -0.35f))) g.FillPath(br, p);
                    for (float y = r.Y; y < r.Bottom; y += 4) for (float x = r.X; x < r.Right; x += 4) { bool a = (((int)((x - r.X) / 4)) + ((int)((y - r.Y) / 4))) % 2 == 0; using (var br = new SolidBrush(a ? Color.FromArgb(70, sh(c, 0.25f)) : Color.FromArgb(60, 0, 0, 0))) g.FillRectangle(br, x, y, 4, 4); }
                    using (var br = new LinearGradientBrush(r, Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 45f)) g.FillPath(br, p);
                    break;
                case 12:  // satin: a soft glow without a hard highlight
                    { var cb = new ColorBlend(3) { Colors = new[] { sh(c, 0.16f), c, sh(c, -0.14f) }, Positions = new[] { 0f, 0.45f, 1f } }; using (var br = new LinearGradientBrush(r, c, c, 90f)) { br.InterpolationColors = cb; g.FillPath(br, p); } using (var br = new LinearGradientBrush(r, Color.FromArgb(0, 255, 255, 255), Color.FromArgb(34, 255, 255, 255), 0f)) g.FillPath(br, p); }
                    break;
                case 13:  // frosted glass: see-through, pale, with a bright edge
                    using (var br = new SolidBrush(Color.FromArgb(255, 18, 14, 36))) g.FillPath(br, p);
                    using (var br = new SolidBrush(Color.FromArgb(165, c))) g.FillPath(br, p);
                    using (var br = new LinearGradientBrush(r, Color.FromArgb(90, 255, 255, 255), Color.FromArgb(10, 255, 255, 255), 60f)) g.FillPath(br, p);
                    for (int i = 0; i < 10; i++) { int hv = MixHash(i, seed); using (var br = new SolidBrush(Color.FromArgb(50, 255, 255, 255))) g.FillEllipse(br, r.X + (hv & 0xFF) / 255f * r.Width, r.Y + ((hv >> 8) & 0xFF) / 255f * r.Height, 2, 2); }
                    break;
                case 14:  // worn: faded, with specks and scratches
                    using (var br = new LinearGradientBrush(r, sh(c, 0.08f), sh(c, -0.14f), 70f)) g.FillPath(br, p);
                    for (int i = 0; i < 34; i++) { int hv = MixHash(i, seed); float px = r.X + (hv & 0xFF) / 255f * r.Width, py = r.Y + ((hv >> 8) & 0xFF) / 255f * r.Height, sz = 1f + ((hv >> 16) & 3) * 0.8f; using (var br = new SolidBrush(Color.FromArgb(64, (hv & 0x1000) != 0 ? 255 : 0, (hv & 0x1000) != 0 ? 255 : 0, (hv & 0x1000) != 0 ? 255 : 0))) g.FillEllipse(br, px, py, sz, sz); }
                    for (int i = 0; i < 3; i++) { int hv = MixHash(100 + i, seed); float px = r.X + (hv & 0xFF) / 255f * r.Width * 0.6f, py = r.Y + ((hv >> 8) & 0xFF) / 255f * r.Height * 0.6f; using (var pen = new Pen(Color.FromArgb(70, 255, 255, 255), 1f)) g.DrawLine(pen, px, py, px + r.Width * 0.35f, py + r.Height * 0.3f); }
                    break;
                case 15:  // neon glow: a bright core (the halo was drawn first)
                    using (var br = new LinearGradientBrush(r, sh(c, 0.35f), c, 90f)) g.FillPath(br, p);
                    break;
            }
            g.Restore(gs);
            using (var pen = new Pen(Color.FromArgb(kind == 3 ? 70 : 130, 255, 255, 255), 1f)) g.DrawPath(pen, p);
        }
    }
    void MixText(Graphics g, string s, float px, Color on, RectangleF r, bool bold) { Txt(g, s, px, MixColours.Luma(on) > 0.6 ? Color.FromArgb(255, 20, 12, 36) : Color.FromArgb(255, 250, 244, 255), r, StringAlignment.Center, bold); }
    // a section of the mixer: always a neat box with a neon outline
    void MixBox(Graphics g, RectangleF r) { FillRR(g, TM.C(255, 8, 4, 20), r, 14); Win.DrawNeonBox(g, r, false); }

    // one coloured circle with its label and code (A, B or the merged colour)
    void MixCircle(Graphics g, RectangleF r, Color c, int kind, string title, string name, Color c2 = default(Color))
    {
        var gs = g.Save(); using (var p = new GraphicsPath()) { p.AddEllipse(r); g.SetClip(p, CombineMode.Intersect); }
        MixSwatch(g, r, c, kind, r.Width / 2f, c2); g.Restore(gs);
        using (var pen = new Pen(Color.FromArgb(230, 255, 255, 255), 2.2f)) g.DrawEllipse(pen, r);
        float fs = Math.Max(10, r.Width * 0.115f); MixText(g, title, fs + 2, c, Rc(r.X, r.Y + r.Height * 0.22f, r.Width, fs + 8), true); MixText(g, MixColours.HexOf(c), fs, c, Rc(r.X, r.Y + r.Height * 0.22f + fs + 8, r.Width, fs + 6), false);
        MixText(g, name.Length > 18 ? name.Substring(0, 17) + "." : name, Math.Max(9, fs - 2), c, Rc(r.X, r.Y + r.Height * 0.22f + 2 * fs + 12, r.Width, fs + 4), false);
    }
    void MixArrow(Graphics g, RectangleF r, Color from, Color to, bool pointRight, string label)
    {
        float ah = Math.Min(r.Height, 54f), cy = r.Y + r.Height / 2, head = ah * 0.7f;
        var pts = pointRight ? new[] { new PointF(r.X, cy - ah * 0.28f), new PointF(r.Right - head, cy - ah * 0.28f), new PointF(r.Right - head, cy - ah * 0.5f), new PointF(r.Right, cy), new PointF(r.Right - head, cy + ah * 0.5f), new PointF(r.Right - head, cy + ah * 0.28f), new PointF(r.X, cy + ah * 0.28f) }
                              : new[] { new PointF(r.Right, cy - ah * 0.28f), new PointF(r.X + head, cy - ah * 0.28f), new PointF(r.X + head, cy - ah * 0.5f), new PointF(r.X, cy), new PointF(r.X + head, cy + ah * 0.5f), new PointF(r.X + head, cy + ah * 0.28f), new PointF(r.Right, cy + ah * 0.28f) };
        using (var br = new LinearGradientBrush(r, pointRight ? from : to, pointRight ? to : from, 0f)) g.FillPolygon(br, pts);
        using (var pen = new Pen(Color.FromArgb(120, 255, 255, 255), 1f)) g.DrawPolygon(pen, pts);
        Txt(g, label, 11, Color.White, Rc(r.X, cy - 9, r.Width - head * 0.4f, 18), StringAlignment.Center, true);
    }
    // the colour wheel (it turns slowly): the hues in a ring, with arrows from the middle to colour A, colour B and the colour they make together
    void MixWheel(Graphics g, RectangleF area)
    {
        float R = Math.Min(area.Width, area.Height) / 2f - 4, cx = area.X + area.Width / 2f, cy = area.Y + area.Height / 2f; float th = Math.Max(10, R * 0.28f); var ring = new RectangleF(cx - R + th / 2, cy - R + th / 2, 2 * R - th, 2 * R - th);
        float rot = mixWheelRot; var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        for (int i = 0; i < 36; i++) using (var pen = new Pen(ThemeDef.Hsl(i * 10, 0.85, 0.52), th)) g.DrawArc(pen, ring, i * 10 - 90 - 0.6f + rot, 10.8f);
        Func<Color, float> ang = c => (float)((c.GetHue() - 90 + rot) * Math.PI / 180);
        Action<Color, string, bool> marker = (c, letter, arrow) =>
        {
            float a = ang(c), ir = R - th / 2; var mp = new PointF(cx + (float)Math.Cos(a) * ir, cy + (float)Math.Sin(a) * ir);
            if (arrow) using (var pen = new Pen(Color.FromArgb(240, 255, 255, 255), 2.4f) { EndCap = LineCap.ArrowAnchor }) g.DrawLine(pen, cx, cy, cx + (float)Math.Cos(a) * (R - th - 6), cy + (float)Math.Sin(a) * (R - th - 6));
            var dot = new RectangleF(mp.X - 11, mp.Y - 11, 22, 22); using (var br = new SolidBrush(c)) g.FillEllipse(br, dot); using (var pen = new Pen(Color.White, 2f)) g.DrawEllipse(pen, dot); Txt(g, letter, 11, MixColours.Luma(c) > 0.6 ? Color.Black : Color.White, dot, StringAlignment.Center, true);
        };
        marker(MixedNow, "M", true); marker(mixA, "A", true); marker(mixB, "B", true);
        using (var br = new SolidBrush(MixedNow)) g.FillEllipse(br, cx - 7, cy - 7, 14, 14); using (var pen = new Pen(Color.White, 1.6f)) g.DrawEllipse(pen, cx - 7, cy - 7, 14, 14); g.SmoothingMode = sm;
    }

    void ShowMixMenu(RectangleF at, int side)
    {
        var m = Win.NewMenu(); int cur = side == 0 ? mixCatA : mixCatB;
        for (int i = 0; i < MixColours.All.Count; i++) { int k = i; var p = MixColours.All[i]; var it = new ToolStripMenuItem(p.Name + (p.Kind == MixColours.Grid ? "  (" + MixColours.ShadeCols * MixColours.ShadeRows + ")" : "  (" + p.Cols.Length + ")")) { Checked = cur == i }; it.Click += (a, b) => { if (side == 0) mixCatA = k; else mixCatB = k; MixChanged(); }; m.Items.Add(it); }
        m.Show(this, new Point((int)at.X, (int)at.Bottom));
    }
    // your name in the colour of your mix (one colour or a fade from A to B); the normal writing when the profile is not following the mix
    void NameText(Graphics g, string s, float px, RectangleF r, Color normal, bool bold)
    {
        if (ProfileStore.NameRgb < 0) { Txt(g, s, px, normal, r, StringAlignment.Near, bold); return; }
        Color c1 = ProfileStore.ColorOf(ProfileStore.NameRgb); if (ProfileStore.NameRgb2 < 0 || NoTextDraw) { Txt(g, s, px, c1, r, StringAlignment.Near, bold); return; }
        float real = GfxCache.Grow(px); var f = GfxCache.Exact(real, bold); float w = Math.Max(20f, Math.Min(r.Width, g.MeasureString(s, f, 100000, GfxCache.Fmt(StringAlignment.Near, false)).Width - 4));
        using (var br = new LinearGradientBrush(new RectangleF(r.X, r.Y, w, Math.Max(2f, r.Height)), c1, ProfileStore.ColorOf(ProfileStore.NameRgb2), 0f)) g.DrawString(s, f, br, r, GfxCache.Fmt(StringAlignment.Near, true));
    }
    internal string MixUseAsTheme()
    {
        Color m = MixedNow; double h, s, l; ThemeDef.ToHsl(m, out h, out s, out l); string bg = MixColours.HexOf(ThemeDef.Hsl(h, 0.5, 0.05)).Substring(1);
        string text = "name=Mix " + MixColours.HexOf(m).Substring(1) + "\nviolet=" + MixColours.HexOf(m).Substring(1) + "\ncyan=" + MixColours.HexOf(mixB).Substring(1) + "\npink=" + MixColours.HexOf(mixA).Substring(1) + "\nbg0=" + bg;
        ThemeDef t; string err = Themes.Import(text, "Mix", out t); if (err != null) { Toast(err); return err; } int prev = Themes.Index; Themes.Apply(Themes.IndexOf(t.Name)); Prefs.Changed();
        mixStatus = "Theme made from your mix and in use: " + t.Name + "."; Toast(mixStatus); OfferRevert("Theme " + t.Name + " is on", () => { Themes.Apply(prev); Prefs.Changed(); }); return null;
    }
    internal void MixCopyCode() { try { Clipboard.SetText(MixColours.HexOf(MixedNow)); mixStatus = MixColours.HexOf(MixedNow) + " copied."; Toast(mixStatus); } catch { mixStatus = "Could not copy (another program is using the clipboard)."; } }

    // one side of the palette: its own STYLE drop bar and its colours (the left side is colour A, which merges into the colour of the right side, colour B); returns the y under it
    float MixSide(Graphics g, float x, float y, float w, int side, float vt, float vb)
    {
        Func<float, float, bool> vis = (a, b) => b > vt - 6 && a < vb + 6; int cat = side == 0 ? mixCatA : mixCatB; var pal = MixColours.All[Math.Max(0, Math.Min(MixColours.All.Count - 1, cat))]; Color cur = side == 0 ? mixA : mixB; string curName = side == 0 ? mixNameA : mixNameB;
        float px = x + 14, pw = w - 28, yy = y + 12; string hover = "";
        if (vis(yy, yy + 26)) { Txt(g, side == 0 ? "COLOUR A  -  THE LEFT SIDE" : "COLOUR B  -  THE RIGHT SIDE", 13, Ink, Rc(px, yy, pw - 130, 22), StringAlignment.Near, true); var chip = Rc(px + pw - 120, yy, 120, 22); FillRR(g, cur, chip, 6); StrokeRR(g, Color.FromArgb(200, 255, 255, 255), chip, 6, 1.2f); MixText(g, MixColours.HexOf(cur), 11, cur, chip, true); }
        yy += 26; if (vis(yy, yy + 18)) Txt(g, side == 0 ? "This colour merges into the colour on the right." : "The colour on the left merges into this one.", 11, DimC, Rc(px, yy, pw, 18), StringAlignment.Near, false); yy += 24;
        if (vis(yy, yy + 32)) { var dd = Rc(px, yy, Math.Min(pw, 300), 32); Btn(g, dd, "STYLE: " + pal.Name.ToUpper() + "   v", 1, () => ShowMixMenu(dd, side), 12); Hint2(dd, "Choose the style of colours on this side: every shade, gloss, metallic, metals, chrome, chameleon, candy, pearlescent, holographic, glitter, carbon, satin, matte, frosted glass, worn, neon and more"); }
        yy += 42;
        if (pal.Kind == MixColours.Grid)
        {
            float cell = Math.Max(8, (pw - (MixColours.ShadeCols - 1) * 1.5f) / MixColours.ShadeCols), step = cell + 1.5f;
            for (int r = 0; r < MixColours.ShadeRows; r++) { float ry = yy + r * step; if (!vis(ry, ry + cell)) continue; for (int c = 0; c < MixColours.ShadeCols; c++) { var cr = Rc(px + c * step, ry, cell, cell); Color col = MixColours.Shade(c, r); string nm = MixColours.ShadeName(c, r); using (var br = new SolidBrush(col)) g.FillRectangle(br, cr); if (Hot(cr)) { hover = nm + "  " + MixColours.HexOf(col); using (var pen = new Pen(Color.White, 2f)) g.DrawRectangle(pen, cr.X, cr.Y, cr.Width, cr.Height); } int cc = c, rr2 = r, sd = side; var h = Reg(cr, () => MixSet(sd, MixColours.Shade(cc, rr2), 0, MixColours.ShadeName(cc, rr2))); h.OnRight = () => MixSet(1 - sd, MixColours.Shade(cc, rr2), 0, MixColours.ShadeName(cc, rr2)); } }
            yy += MixColours.ShadeRows * step + 6;
        }
        else
        {
            float cell = 36, gap = 7; int cols = Math.Max(3, (int)((pw + gap) / (cell + gap))); int rows = (pal.Cols.Length + cols - 1) / cols;
            for (int i = 0; i < pal.Cols.Length; i++)
            {
                float ry = yy + (i / cols) * (cell + gap); if (!vis(ry, ry + cell)) continue; var cr = Rc(px + (i % cols) * (cell + gap), ry, cell, cell); int k = i, sd = side; Color s2 = pal.Cols2 != null ? pal.Cols2[i] : Color.Empty; MixSwatch(g, cr, pal.Cols[i], pal.Kind, 8, s2);
                if (pal.Cols[i] == cur && pal.Names[i] == curName) using (var p = RR(RectangleF.Inflate(cr, 3, 3), 10)) using (var pen = new Pen(NeonCyan, 2.4f)) g.DrawPath(pen, p);
                if (Hot(cr)) { hover = pal.Names[i] + "  " + MixColours.HexOf(pal.Cols[i]) + (s2.IsEmpty ? "" : " to " + MixColours.HexOf(s2)); using (var p = RR(RectangleF.Inflate(cr, 2, 2), 9)) using (var pen = new Pen(Color.White, 2.2f)) g.DrawPath(pen, p); }
                var h = Reg(cr, () => MixSet(sd, pal.Cols[k], pal.Kind, pal.Names[k], s2)); h.OnRight = () => MixSet(1 - sd, pal.Cols[k], pal.Kind, pal.Names[k], s2);
            }
            yy += rows * (cell + gap) + 2;
        }
        if (side == 0) mixHoverA = hover; else mixHoverB = hover; string hv = side == 0 ? mixHoverA : mixHoverB;
        Txt(g, hv.Length > 0 ? hv : "Point at a colour for its name and code.", 11, hv.Length > 0 ? Ink : DimC, Rc(px, yy, pw, 20), StringAlignment.Near, hv.Length > 0); yy += 30;
        return yy;
    }

    // the box itself; returns the y under it. vt / vb: the part of the page that is on screen (the rest is not drawn and has no buttons)
    float PaintColourMixer(Graphics g, float x, float y, float w, float vt, float vb)
    {
        MixEnsure(); if (mixDirty != DateTime.MinValue && (DateTime.Now - mixDirty).TotalMilliseconds > 700) MixFlush();
        var box = Rc(x, y, w, mixH); using (var p = RR(box, 18)) { using (var br = new SolidBrush(TM.C(255, 12, 6, 28))) g.FillPath(br, p); } Win.DrawNeonBox(g, box, false);
        float px = x + 18, pw = w - 36, yy = y + 14; Func<float, float, bool> vis = (a, b) => b > vt - 6 && a < vb + 6;
        Txt(g, "COLOUR MIXER", 18, Ink, Rc(px, yy, 400, 28), StringAlignment.Near, true); yy += 32;
        TxtWrap(g, "Choose colour A on the left and colour B on the right: A merges into B. Whatever you change here is your profile colour. " + MixColours.Total + " colours in every finish.", 12, DimC, Rc(px, yy, pw, 36)); yy += 44;
        // ---- A + B = merged, the mix, swap and the two switches ----
        var merged = MixedNow; float dA = Math.Max(72, Math.Min(124, pw * 0.11f)), dM = dA * 1.22f, gapA = Math.Max(50, Math.Min(170, pw * 0.12f)), rowW = dA + gapA + dM + gapA + dA, rowH = dM; var b1 = Rc(px, yy, pw, rowH + 24 + 44 + 44 + 12); MixBox(g, b1);
        { float rx = px + (pw - rowW) / 2, ty = yy + 12, cyC = ty + rowH / 2;
          if (vis(ty, ty + rowH))
          {
              MixCircle(g, Rc(rx, cyC - dA / 2, dA, dA), mixA, mixKA, "COLOUR A", mixNameA, mixA2);
              MixArrow(g, Rc(rx + dA + 6, cyC - 27, gapA - 12, 54), mixA, merged, true, (int)Math.Round((1 - mixT) * 100) + "% A");
              MixCircle(g, Rc(rx + dA + gapA, ty, dM, dM), merged, MixedKind, "MERGED", "A + B", MixedSecond);
              MixArrow(g, Rc(rx + dA + gapA + dM + 6, cyC - 27, gapA - 12, 54), merged, mixB, false, (int)Math.Round(mixT * 100) + "% B");
              MixCircle(g, Rc(rx + dA + gapA + dM + gapA, cyC - dA / 2, dA, dA), mixB, mixKB, "COLOUR B", mixNameB, mixB2);
          }
          float cy2 = ty + rowH + 12; if (vis(cy2, cy2 + 34))
          {
              var sw = Rc(px + 14, cy2, 88, 32); Btn(g, sw, "SWAP", 0, () => MixSwap(), 12); Hint2(sw, "Swap colour A and colour B");
              float lx = sw.Right + 18; Txt(g, "MIX", 12, DimC, Rc(lx, cy2, 40, 32), StringAlignment.Near, true); var sr = Rc(lx + 44, cy2 + 4, Math.Max(80, px + pw - 14 - lx - 44 - 160), 24); Slider(g, sr, mixT, f => MixSetT(f), merged); Hint2(sr, "How much of colour B is in the mix (left is all A, right is all B)");
              Txt(g, (int)Math.Round((1 - mixT) * 100) + "% A / " + (int)Math.Round(mixT * 100) + "% B", 12, Ink, Rc(sr.Right + 10, cy2, 150, 32), StringAlignment.Near, true);
          }
          float cy3 = cy2 + 42; if (vis(cy3, cy3 + 34))
          {
              float cwid = Math.Min(330, (pw - 28 - 10) / 2f); var t1 = Rc(px + 14, cy3, cwid, 32); var t2 = Rc(t1.Right + 10, cy3, cwid, 32);
              Chip(g, t1, "PROFILE COLOUR = MY MIX: " + (mixFollow ? "ON" : "OFF"), mixFollow, () => MixSetFollow(!mixFollow), 1); Hint2(t1, "ON: whatever you change here is your profile colour (banner, rings, accents). OFF: your profile uses the colour of your theme again");
              Chip(g, t2, "FADE MY NAME A TO B: " + (mixFade ? "ON" : "OFF"), mixFade, () => MixSetFade(!mixFade), 1); Hint2(t2, "ON: your name on your profile fades from colour A to colour B. OFF: it takes the merged colour");
          } }
        yy += b1.Height + 14;
        // ---- the two sides of the palette ----
        bool twoCols = pw >= 760; float colW = twoCols ? (pw - 14) / 2f : pw;
        { float hA = MixSideHeight(colW, mixCatA), hB = MixSideHeight(colW, mixCatB); float rowH2 = twoCols ? Math.Max(hA, hB) : hA;
          var bA = Rc(px, yy, colW, twoCols ? rowH2 : hA); MixBox(g, bA); float endA = MixSide(g, bA.X, bA.Y, bA.Width, 0, vt, vb);
          var bB = twoCols ? Rc(px + colW + 14, yy, colW, rowH2) : Rc(px, yy + hA + 14, colW, hB); MixBox(g, bB); float endB = MixSide(g, bB.X, bB.Y, bB.Width, 1, vt, vb);
          yy = Math.Max(bA.Bottom, bB.Bottom) + 14; }
        // ---- the mixing guide: gradient and steps, the wheel (it turns), the pairs: three boxes, drawn first, then what is in them ----
        bool three = pw >= 900; float cw3 = three ? (pw - 2 * 14) / 3f : pw; float ws = Math.Min(cw3 - 24, 210f); float gh1 = 224f, gh2 = ws + 24f, gh3 = 290f, hMax = Math.Max(gh1, Math.Max(gh2, gh3));
        float gt1 = yy, gt2 = three ? yy : yy + gh1 + 14, gt3 = three ? yy : gt2 + gh2 + 14; float gx1 = px, gx2 = three ? px + cw3 + 14 : px, gx3 = three ? px + 2 * (cw3 + 14) : px;
        if (three) { gh1 = gh2 = gh3 = hMax; }
        MixBox(g, Rc(gx1, gt1, cw3, gh1)); MixBox(g, Rc(gx2, gt2, cw3, gh2)); MixBox(g, Rc(gx3, gt3, cw3, gh3));
        { // gradient + steps
            float by = gt1 + 12; float bx = gx1 + 12, bw = cw3 - 24; Txt(g, "HORIZONTAL GRADIENT MIX", 12, DimC, Rc(bx, by, bw, 18), StringAlignment.Near, true); by += 22; var gr = Rc(bx, by, bw, 36);
            if (vis(gr.Y, gr.Bottom)) { using (var p = RR(gr, 6)) { using (var br = new LinearGradientBrush(gr, mixA, mixB, 0f)) g.FillPath(br, p); using (var pen = new Pen(Color.FromArgb(150, 255, 255, 255), 1f)) g.DrawPath(pen, p); } }
            by += 42; Txt(g, MixColours.HexOf(mixA), 10, DimC, Rc(bx, by, 90, 16), StringAlignment.Near, false); Txt(g, "A TO B", 10, DimC, Rc(bx + 90, by, bw - 180, 16), StringAlignment.Center, false); Txt(g, MixColours.HexOf(mixB), 10, DimC, Rc(bx + bw - 90, by, 90, 16), StringAlignment.Far, false); by += 28;
            Txt(g, "STEPPED COLOUR BLEND", 12, DimC, Rc(bx, by, bw, 18), StringAlignment.Near, true); by += 24; float sw = (bw - 4 * 8) / 5f; string[] lb = { "100% A", "75 / 25", "50 / 50", "25 / 75", "100% B" };
            for (int i = 0; i < 5; i++) { var sr = Rc(bx + i * (sw + 8), by + 18, sw, 44); Color sc = MixColours.Mix(mixA, mixB, i / 4f); if (vis(sr.Y, sr.Bottom)) { Txt(g, lb[i], 9, DimC, Rc(sr.X - 6, by, sr.Width + 12, 16), StringAlignment.Center, false); using (var p = RR(sr, 6)) { using (var br = new SolidBrush(sc)) g.FillPath(br, p); using (var pen = new Pen(Color.FromArgb(150, 255, 255, 255), 1f)) g.DrawPath(pen, p); } Txt(g, MixColours.HexOf(sc), 9, DimC, Rc(sr.X - 6, sr.Bottom + 3, sr.Width + 12, 14), StringAlignment.Center, false); } }
        }
        { // the wheel: no title, and it turns slowly
            var wr = Rc(gx2 + (cw3 - ws) / 2, gt2 + (gh2 - ws) / 2, ws, ws);
            if (vis(wr.Y, wr.Bottom)) { mixWheelRot = (float)((Environment.TickCount % 60000) / 60000.0 * 360.0 * 4); MixWheel(g, wr); lastInput = DateTime.Now; btnAnim = true; }
        }
        { // the pairs
            float bx = gx3 + 12, bw = cw3 - 24; float by = gt3 + 12; Txt(g, "COLOUR SWATCH VARIATIONS", 12, DimC, Rc(bx, by, bw, 18), StringAlignment.Near, true); by += 24;
            Action<string, Color, Color, Action> pair = (title, a, b, click) =>
            {
                var row = Rc(bx, by, bw, 58); if (vis(row.Y, row.Bottom))
                {
                    Color mm = MixColours.Mix(a, b, 0.5f); float s = 46; Txt(g, title, 11, Ink, Rc(bx, by - 2, bw, 14), StringAlignment.Near, true); float sy = by + 14;
                    Action<float, Color> sq = (ox, col) => { var r0 = Rc(bx + ox, sy, s, s - 14); using (var p = RR(r0, 5)) { using (var br = new SolidBrush(col)) g.FillPath(br, p); using (var pen = new Pen(Color.FromArgb(150, 255, 255, 255), 1f)) g.DrawPath(pen, p); } Txt(g, MixColours.HexOf(col), 8, DimC, Rc(bx + ox - 8, sy + s - 13, s + 16, 12), StringAlignment.Center, false); };
                    sq(0, a); Txt(g, "+", 14, Ink, Rc(bx + s, sy, 22, s - 14), StringAlignment.Center, true); sq(s + 22, b); Txt(g, "=", 14, Ink, Rc(bx + 2 * s + 22, sy, 22, s - 14), StringAlignment.Center, true); sq(2 * s + 44, mm);
                    if (click != null) { Reg(row, click); Hint2(row, "Use this pair as colour A and colour B"); }
                }
                by += 62;
            };
            pair("Your mix (A + B)", mixA, mixB, null);
            foreach (var pr in MixColours.Pairs) { var p0 = pr; pair(p0[0], MixColours.Hex(p0[1]), MixColours.Hex(p0[2]), () => { MixSet(0, MixColours.Hex(p0[1]), 0, p0[0].Split('+')[0].Trim()); MixSet(1, MixColours.Hex(p0[2]), 0, p0[0].Split('+')[1].Trim()); MixSetT(0.5f); }); }
        }
        yy = three ? gt1 + hMax + 14 : gt3 + gh3 + 14;
        // ---- the colour presets: save the mix and go through the ones you saved ----
        { var pb = Rc(px, yy, pw, 0); float chip = 52, gap = 8; int cols = Math.Max(3, (int)((pw - 28 + gap) / (chip + gap))); int n = MixStore.Presets.Count, rows = Math.Max(1, (n + cols - 1) / cols); float ph = 12 + 24 + 44 + (n == 0 ? 30 : rows * (chip + gap)) + 30 + 6; pb = Rc(px, yy, pw, ph); MixBox(g, pb);
          float qy = pb.Y + 12; Txt(g, "MY COLOUR PRESETS (" + n + ")", 12, DimC, Rc(pb.X + 14, qy, 300, 18), StringAlignment.Near, true); qy += 24;
          if (vis(qy, qy + 36)) { float bw2 = Math.Min(250, (pw - 28 - 3 * 10) / 4f); var s1 = Rc(pb.X + 14, qy, bw2 + 40, 36); var s2 = Rc(s1.Right + 10, qy, bw2 - 40, 36); var s3 = Rc(s2.Right + 10, qy, bw2 - 40, 36); var s4 = Rc(s3.Right + 10, qy, bw2 + 40, 36);
              Btn(g, s1, "SAVE COLOUR PRESET", 2, () => MixPresetSave(), 12); Hint2(s1, "Save the two colours, their finishes and the mix as a preset, so you can bring this merged colour back later");
              Btn(g, s2, "< PREV", n > 0 ? 1 : 0, () => MixPresetStep(-1), 12); Hint2(s2, "Go back through your saved colour presets"); Btn(g, s3, "NEXT >", n > 0 ? 1 : 0, () => MixPresetStep(1), 12); Hint2(s3, "Go forward through your saved colour presets");
              Btn(g, s4, "DELETE THIS PRESET", mixPresetSel >= 0 ? 3 : 0, () => MixPresetDelete(), 12); Hint2(s4, "Delete the colour preset that is picked (the outlined one)"); }
          qy += 44;
          if (n == 0) Txt(g, "Nothing saved yet. Make a colour you like and press SAVE COLOUR PRESET.", 12, DimC, Rc(pb.X + 14, qy, pw - 28, 24), StringAlignment.Near, false);
          else for (int i = 0; i < n; i++)
          {
              float cx0 = pb.X + 14 + (i % cols) * (chip + gap), cy0 = qy + (i / cols) * (chip + gap); if (!vis(cy0, cy0 + chip)) continue; var p = MixStore.Presets[i]; var cr = Rc(cx0, cy0, chip, chip); int ix = i;
              using (var path = RR(cr, 10)) { var gs = g.Save(); g.SetClip(path, CombineMode.Intersect); var left = Rc(cr.X, cr.Y, cr.Width / 2f + 1, cr.Height); var right = Rc(cr.X + cr.Width / 2f, cr.Y, cr.Width / 2f, cr.Height); using (var br = new LinearGradientBrush(cr, p.A, p.B, 0f)) g.FillPath(br, path); g.Restore(gs); }
              var dot = Rc(cr.X + chip / 2 - 9, cr.Y + chip / 2 - 9, 18, 18); using (var br = new SolidBrush(MixColours.Mix(p.A, p.B, p.T))) g.FillEllipse(br, dot); using (var pen = new Pen(Color.White, 1.6f)) g.DrawEllipse(pen, dot);
              using (var pth = RR(cr, 10)) using (var pen = new Pen(i == mixPresetSel ? NeonCyan : Color.FromArgb(150, 255, 255, 255), i == mixPresetSel ? 3f : 1f)) g.DrawPath(pen, pth);
              if (Hot(cr)) Hint2(cr, p.Name + "  (" + MixColours.HexOf(p.A) + " to " + MixColours.HexOf(p.B) + ", " + MixColours.HexOf(MixColours.Mix(p.A, p.B, p.T)) + ")");
              Reg(cr, () => MixPresetLoad(ix));
          }
          yy = pb.Bottom + 14; }
        // ---- use it: a theme, or the code ----
        { var ub = Rc(px, yy, pw, 12 + 38 + 12 + 26); MixBox(g, ub); float uy = ub.Y + 12;
          if (vis(uy, uy + 40)) { float bw = Math.Min(280, (pw - 28 - 10) / 2f); var u1 = Rc(ub.X + 14, uy, bw, 38); var u2 = Rc(u1.Right + 10, uy, bw, 38);
              Btn(g, u1, "USE AS A THEME", 2, () => MixUseAsTheme(), 12); Hint2(u1, "Make a theme for the whole program from your mix (the merged colour, A and B). REVERT puts the old one back");
              Btn(g, u2, "COPY THE HEX CODE", 1, () => MixCopyCode(), 12); Hint2(u2, "Copy the code of the merged colour (like #A79CC4)"); }
          Txt(g, mixStatus.Length > 0 ? mixStatus : "Your profile colour is " + (mixFollow ? "your mix: " + MixColours.HexOf(MixedNow) : "the colour of your theme") + ".", 12, mixStatus.Length > 0 ? Ink : DimC, Rc(ub.X + 14, uy + 46, pw - 28, 20), StringAlignment.Near, false);
          yy = ub.Bottom; }
        mixH = Math.Max(300, yy + 14 - y); return y + mixH;
    }
    // the height of one side of the palette (the box round it), worked out from its style so both sides can be the same height
    float MixSideHeight(float w, int cat)
    {
        var pal = MixColours.All[Math.Max(0, Math.Min(MixColours.All.Count - 1, cat))]; float pw = w - 28, h = 12 + 26 + 24 + 42;
        if (pal.Kind == MixColours.Grid) { float cell = Math.Max(8, (pw - (MixColours.ShadeCols - 1) * 1.5f) / MixColours.ShadeCols); h += MixColours.ShadeRows * (cell + 1.5f) + 6; }
        else { float cell = 36, gap = 7; int cols = Math.Max(3, (int)((pw + gap) / (cell + gap))); int rows = (pal.Cols.Length + cols - 1) / cols; h += rows * (cell + gap) + 2; }
        return h + 30 + 6;
    }
}
