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
    public const int Plain = 0, Metal = 1, Pearl = 2, Matte = 3, Grid = 4, Chameleon = 5, Candy = 6, Chrome = 7, Brushed = 8, Holo = 9, Glitter = 10, Carbon = 11, Satin = 12, Frost = 13, Worn = 14, Glow = 15, Translucent = 16;
    public static Color Hex(string h) { h = h.TrimStart('#'); return Color.FromArgb(255, Convert.ToInt32(h.Substring(0, 2), 16), Convert.ToInt32(h.Substring(2, 2), 16), Convert.ToInt32(h.Substring(4, 2), 16)); }
    public static string HexOf(Color c) { return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"); }
    // the colour two colours make when they merge: the hue travels the short way round the wheel (so orange and blue pass through yellow and green, not grey mud), the saturation stays full and a little light is added in the middle
    public static Color Blend(Color a, Color b, float t)
    {
        t = Math.Max(0f, Math.Min(1f, t)); if (t <= 0f) return Color.FromArgb(255, a.R, a.G, a.B); if (t >= 1f) return Color.FromArgb(255, b.R, b.G, b.B);
        double h1, s1, l1, h2, s2, l2; ThemeDef.ToHsl(a, out h1, out s1, out l1); ThemeDef.ToHsl(b, out h2, out s2, out l2); if (s1 < 0.06) h1 = h2; else if (s2 < 0.06) h2 = h1;
        double d = h2 - h1; if (d > 180) d -= 360; if (d < -180) d += 360; double h = (h1 + d * t + 720) % 360, bump = Math.Sin(Math.PI * t);
        double sat = Math.Min(1.0, s1 + (s2 - s1) * t + 0.08 * bump), lit = Math.Max(0.0, Math.Min(0.92, l1 + (l2 - l1) * t + 0.07 * bump)); return ThemeDef.Hsl(h, sat, lit);
    }
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
        P("Translucent (see-through)", Translucent, "Clear Red#FF4D4D|Clear Orange#FF9F40|Clear Yellow#FFE55C|Clear Lime#9BE564|Clear Green#3DDC84|Clear Aqua#3DE0C8|Clear Cyan#3DD6F5|Clear Blue#4D8DFF|Clear Indigo#6B5BFF|Clear Purple#B05CFF|Clear Pink#FF5CC8|Clear Rose#FF7A9E|Clear Smoke#9AA0AA|Clear White#F2F6FA|Clear Black#2A2D35|Clear Gold#E8C65A"),
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
    public static int CatA = -1, CatB = -1; public static bool Follow = true, Fade = true, AOn = true, BOn = true, Loaded; public static float TS;   // TS: how far the one colour that is on has faded to dark (0 = as it is)
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
                if (k == "state") { State = p; int n; if (rest.Length > 0 && int.TryParse(rest[0], out n)) CatA = n; if (rest.Length > 1 && int.TryParse(rest[1], out n)) CatB = n; if (rest.Length > 2) Follow = rest[2] != "0"; if (rest.Length > 3) Fade = rest[3] != "0"; if (rest.Length > 4) AOn = rest[4] != "0"; if (rest.Length > 5) BOn = rest[5] != "0"; float ts; if (rest.Length > 6 && float.TryParse(rest[6], NumberStyles.Float, CultureInfo.InvariantCulture, out ts)) TS = Math.Max(0f, Math.Min(1f, ts)); if (!AOn && !BOn) AOn = true; }
                else if (k == "preset" && Presets.Count < 80) Presets.Add(p);
            }
        }
        catch { }
    }
    public static void Save()
    {
        if (!Loaded) Load();   // never write before reading: the saved colour mixes would be lost
        try { Directory.CreateDirectory(StudioEnv.DataDir); var sb = new StringBuilder(); sb.Append(Line("state", State, CatA, CatB, Follow ? "1" : "0", Fade ? "1" : "0", AOn ? "1" : "0", BOn ? "1" : "0", TS.ToString("0.###", CultureInfo.InvariantCulture))).Append('\n'); foreach (var p in Presets) sb.Append(Line("preset", p)).Append('\n'); Win.WriteAtomic(FileP, sb.ToString(), new UTF8Encoding(false)); } catch { }
    }
}

partial class StudioForm
{
    Color mixA = Color.FromArgb(255, 255, 107, 107), mixB = Color.FromArgb(255, 78, 205, 196), mixA2 = Color.Empty, mixB2 = Color.Empty; int mixKA = 0, mixKB = 0, mixCatA = 1, mixCatB = 1, mixPresetSel = -1; float mixT = 0.5f, mixH = 1500f, mixWheelRot; string mixNameA = "Coral", mixNameB = "Turquoise", mixHoverA = "", mixHoverB = "", mixStatus = "";
    bool mixFollow = true, mixFade = true, mixLoaded, mixAOn = true, mixBOn = true; float mixTS; DateTime mixDirty = DateTime.MinValue;
    // colour A and colour B can each be switched off: with one off the mix is just the other colour (one colour only)
    float MixTe { get { return !mixAOn ? 1f : (!mixBOn ? 0f : mixT); } }
    bool MixSolo { get { return mixAOn != mixBOn; } }   // only one of the two colours is on
    Color MixSoloColour { get { return mixAOn ? mixA : mixB; } }
    static Color MixDark(Color c) { return Color.FromArgb(255, (int)(c.R * 0.1f), (int)(c.G * 0.1f), (int)(c.B * 0.1f)); }
    Color MixEndA { get { return MixSolo ? MixSoloColour : mixA; } }          // the left end of the gradient: colour A, or the one colour as it is
    Color MixEndB { get { return MixSolo ? MixDark(MixSoloColour) : mixB; } }   // the right end: colour B, or the one colour faded to dark
    float MixPos { get { return MixSolo ? mixTS : mixT; } }                     // where the logo rides on the gradient
    public Color MixedNow { get { return MixSolo ? MixColours.Blend(MixSoloColour, MixDark(MixSoloColour), mixTS) : MixColours.Blend(mixA, mixB, mixT); } }
    // the merged colour wears the finish of A and B when they are the same (two chameleons make a chameleon); otherwise it is plain
    int MixedKind { get { return !mixAOn ? mixKB : (!mixBOn ? mixKA : (mixKA != 0 ? mixKA : mixKB)); } }   // the finish of the merged colour: the finish of A (or of B when A is plain)
    Color MixedSecond { get { if (!mixAOn || !mixBOn) { int k1 = !mixAOn ? mixKB : mixKA; Color c1 = !mixAOn ? (mixB2.IsEmpty ? mixB : mixB2) : (mixA2.IsEmpty ? mixA : mixA2); return k1 == MixColours.Chameleon ? c1 : Color.Empty; } if (mixKA != mixKB) return Color.Empty; Color a2 = mixA2.IsEmpty ? mixA : mixA2, b2 = mixB2.IsEmpty ? mixB : mixB2; return mixKA == MixColours.Chameleon ? MixColours.Blend(a2, b2, mixT) : Color.Empty; } }

    void MixEnsure()
    {
        if (mixLoaded) return; mixLoaded = true; MixStore.Load(); var s = MixStore.State; mixA = s.A; mixA2 = s.A2; mixKA = s.KA; mixNameA = s.NA; mixB = s.B; mixB2 = s.B2; mixKB = s.KB; mixNameB = s.NB; mixT = s.T;
        int def = MixColours.All.FindIndex(p => p.Name == "Classic gloss"); if (def < 1) def = 1; mixCatA = MixStore.CatA >= 0 && MixStore.CatA < MixColours.All.Count ? MixStore.CatA : def; mixCatB = MixStore.CatB >= 0 && MixStore.CatB < MixColours.All.Count ? MixStore.CatB : def; mixTS = MixStore.TS; mixFollow = MixStore.Follow; mixFade = MixStore.Fade; mixAOn = MixStore.AOn; mixBOn = MixStore.BOn; if (!mixAOn && !mixBOn) mixAOn = true;
    }
    // a colour that can be read on the dark pages (the name is written in it)
    static Color MixLegible(Color c) { for (int i = 0; i < 8 && MixColours.Luma(c) < 0.38; i++) c = MixColours.Mix(c, Color.White, 0.18f); return c; }
    // the profile follows the mix: whatever you change here IS your profile colour (the banner, the rings, the accents), and your name fades from A to B (or takes the merged colour)
    internal void MixApplyProfile()
    {
        if (!mixFollow) return; ProfileStore.Accent = ProfileStore.Pack(MixedNow); palSeen = ProfileStore.Accent;
        if (mixFade && mixAOn && mixBOn) { ProfileStore.NameRgb = ProfileStore.Pack(MixLegible(mixA)); ProfileStore.NameRgb2 = ProfileStore.Pack(MixLegible(mixB)); } else if (mixFade) { ProfileStore.NameRgb = ProfileStore.Pack(MixLegible(mixAOn ? mixA : mixB)); ProfileStore.NameRgb2 = -1; /* one colour only: the name is in it */ } else { ProfileStore.NameRgb = -1; ProfileStore.NameRgb2 = -1; }   // FADE OFF: the name is in the normal colour (white), whether the profile colour is on or off
    }
    internal void MixChanged() { mixDirty = DateTime.Now; MixApplyProfile(); ApplyProfileLook(); }
    internal void MixFlush()
    {   // saved and sent a moment after the last change (not on every step of a slider)
        mixDirty = DateTime.MinValue; var s = MixStore.State; s.A = mixA; s.A2 = mixA2; s.KA = mixKA; s.NA = mixNameA; s.B = mixB; s.B2 = mixB2; s.KB = mixKB; s.NB = mixNameB; s.T = mixT; MixStore.CatA = mixCatA; MixStore.CatB = mixCatB; MixStore.Follow = mixFollow; MixStore.Fade = mixFade; MixStore.AOn = mixAOn; MixStore.BOn = mixBOn; MixStore.TS = mixTS; MixStore.Save();
        if (mixFollow) { ProfileStore.Save(); QueueRepublish(); }
    }
    internal void MixSet(int side, Color c, int kind, string name, Color c2 = default(Color)) { if (side == 0) { mixA = c; mixKA = kind; mixNameA = name; mixA2 = c2; } else { mixB = c; mixKB = kind; mixNameB = name; mixB2 = c2; } mixStatus = ""; mixPresetSel = -1; MixChanged(); }
    internal void MixSetFollow(bool on)
    {
        mixFollow = on; if (on) MixApplyProfile(); else { ProfileStore.Accent = -1; palSeen = ProfileStore.Accent; ProfileStore.NameRgb = -1; ProfileStore.NameRgb2 = -1; }
        mixStatus = on ? "Your profile colour is your mix: " + MixColours.HexOf(MixedNow) + "." : "Your profile is back to the colour of your theme."; mixDirty = DateTime.Now; if (on) MixApplyProfile(); MixFlush(); ApplyProfileLook(); Toast(mixStatus);
    }
    // colour A and colour B each have an ON / OFF switch (one of them always stays on), and USE A ONLY / USE B ONLY make the profile one colour
    internal void MixSetOn(int side, bool on)
    {
        if (!on && !(side == 0 ? mixBOn : mixAOn)) { mixStatus = "One colour has to stay on."; Toast(mixStatus); return; }
        if (side == 0) mixAOn = on; else mixBOn = on; mixPresetSel = -1; mixStatus = (side == 0 ? "Colour A" : "Colour B") + (on ? " is on." : " is off: the mix is " + (side == 0 ? "colour B" : "colour A") + " only."); MixChanged(); MixFlush();
    }
    internal void MixUseOnly(int side)
    {   // a switch: press it again and both colours are on again
        bool already = side == 0 ? (mixAOn && !mixBOn) : (mixBOn && !mixAOn);
        if (already) { mixAOn = mixBOn = true; mixStatus = "Both colours are on again: A merges into B."; }
        else { mixAOn = side == 0; mixBOn = side == 1; mixStatus = "Your profile uses colour " + (side == 0 ? "A" : "B") + " only: slide the logo to fade it to dark (all the way left it stays as it is). Press USE " + (side == 0 ? "A" : "B") + " ONLY again to turn both back on."; }
        mixPresetSel = -1; MixChanged(); MixFlush(); Toast(mixStatus);
    }
    internal void MixSetFade(bool on) { mixFade = on; MixApplyProfile(); mixStatus = on ? "Your name fades from colour A to colour B." : "Your name is in the normal colour (white)."; MixFlush(); Toast(mixStatus); }
        // the style (the palette) that holds a colour: so a side always opens on the palette of the colour that is on it
    static int MixCatFor(int kind, string name, int fallback) { var all = MixColours.All; for (int i = 0; i < all.Count; i++) if (all[i].Kind == kind && all[i].Names != null && Array.IndexOf(all[i].Names, name) >= 0) return i; return fallback; }
    internal void MixSwap() { int tcat = mixCatA; mixCatA = mixCatB; mixCatB = tcat; var tc = mixA; var tc2 = mixA2; int tk = mixKA; string tn = mixNameA; mixA = mixB; mixA2 = mixB2; mixKA = mixKB; mixNameA = mixNameB; mixB = tc; mixB2 = tc2; mixKB = tk; mixNameB = tn; mixPresetSel = -1; MixChanged(); }
    internal void MixSetTS(float t) { mixTS = (float)Math.Round(Math.Max(0f, Math.Min(1f, t)) * 100) / 100f; mixPresetSel = -1; MixChanged(); }
    internal void MixSetPos(float t) { if (MixSolo) MixSetTS(t); else MixSetT(t); }
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
        if (i < 0 || i >= MixStore.Presets.Count) return; var p = MixStore.Presets[i]; mixAOn = mixBOn = true; mixA = p.A; mixA2 = p.A2; mixKA = p.KA; mixNameA = p.NA; mixB = p.B; mixB2 = p.B2; mixKB = p.KB; mixNameB = p.NB; mixT = p.T; mixPresetSel = i; mixCatA = MixCatFor(p.KA, p.NA, mixCatA); mixCatB = MixCatFor(p.KB, p.NB, mixCatB); MixChanged(); MixFlush(); mixStatus = "Colour preset " + (i + 1) + " of " + MixStore.Presets.Count + ": " + p.Name + " (" + MixColours.HexOf(MixedNow) + ")."; }
    internal void MixPresetStep(int d) { int n = MixStore.Presets.Count; if (n == 0) { mixStatus = "No colour presets saved yet: press SAVE COLOUR PRESET."; return; } int i = mixPresetSel < 0 ? (d > 0 ? 0 : n - 1) : (mixPresetSel + d + n) % n; MixPresetLoad(i); }
    internal void MixPresetDelete()
    {
        if (mixPresetSel < 0 || mixPresetSel >= MixStore.Presets.Count) { mixStatus = "Pick a saved preset first (click one, or use PREV and NEXT), then DELETE."; return; }
        string nm = MixStore.Presets[mixPresetSel].Name; if (!Sure("delete the colour preset \"" + nm + "\"")) return; MixStore.Presets.RemoveAt(mixPresetSel); mixPresetSel = -1; MixFlush(); mixStatus = "Deleted the colour preset \"" + nm + "\"."; Toast(mixStatus);
    }

    static int MixHash(int a, int b) { unchecked { int h = a * 73856093 ^ b * 19349663; h ^= h >> 13; h *= 1274126177; return h ^ (h >> 16); } }
    // one swatch in the finish of its family: plain gloss, metallic, chrome (a mirror with a horizon), chameleon (two colours that shift), candy, brushed, pearlescent, holographic, glitter, carbon, satin, matte, frosted glass, worn, neon glow
    static float MixPhase() { return (Environment.TickCount % 3200) / 3200f; }
    // phase of one swatch: a wave of light that crosses the whole palette
    static float MixWave(float x, float y, float span) { float v = MixPhase() - (x + y) / Math.Max(60f, span); return v - (float)Math.Floor(v); }
    static bool MixShines(int kind) { return kind == 1 || kind == 2 || kind == 5 || kind == 7 || kind == 8 || kind == 9 || kind == 10 || kind == 12; }
    // light on the top edge and shade underneath: the look of a rounded piece of metal
    void MixBevel(Graphics g, GraphicsPath p, RectangleF r, int hi)
    {
        var cb = new ColorBlend(4) { Colors = new[] { Color.FromArgb(hi, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(0, 0, 0, 0), Color.FromArgb(90, 0, 0, 0) }, Positions = new[] { 0f, 0.42f, 0.56f, 1f } };
        using (var br = new LinearGradientBrush(r, Color.White, Color.Black, 90f)) { br.InterpolationColors = cb; g.FillPath(br, p); }
        using (var pen = new Pen(Color.FromArgb(hi / 2, 255, 255, 255), 1f)) g.DrawLine(pen, r.X + r.Height * 0.25f, r.Y + 1.5f, r.Right - r.Height * 0.25f, r.Y + 1.5f);
    }
    // a bright stripe sliding across (phase 0 to 1)
    void MixSheen(Graphics g, RectangleF r, float phase, int alpha)
    {
        float w = Math.Max(10f, r.Width * 0.55f), x0 = r.X - w + (r.Width + 2 * w) * phase; var cb = new ColorBlend(3) { Colors = new[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(alpha, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) }, Positions = new[] { 0f, 0.5f, 1f } };
        var band = new RectangleF(x0, r.Y - 1, w, r.Height + 2); using (var br = new LinearGradientBrush(band, Color.White, Color.White, 0f)) { br.InterpolationColors = cb; g.FillRectangle(br, band); }
        lastInput = DateTime.Now; btnAnim = true;
    }
    // the finish drawn OVER something that already has its colour (the gradient bar and the steps): bevel, wash, sparkle and sheen
    void MixFinishOver(Graphics g, RectangleF r, float rad, int kind, float phase)
    {
        if (kind == 0 || kind >= 16 || kind == 3) return; var gs = g.Save();
        using (var p = RR(r, rad))
        {
            g.SetClip(p, CombineMode.Intersect);
            if (kind == 1 || kind == 5 || kind == 7 || kind == 8 || kind == 12 || kind == 6) MixBevel(g, p, r, kind == 7 ? 170 : 125);
            if (kind == 2 || kind == 9) { var cb = new ColorBlend(5) { Colors = new[] { Color.FromArgb(80, 255, 170, 230), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(80, 150, 210, 255), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(80, 170, 255, 210) }, Positions = new[] { 0f, 0.28f, 0.5f, 0.72f, 1f } }; using (var br = new LinearGradientBrush(r, Color.White, Color.White, 20f)) { br.InterpolationColors = cb; g.FillPath(br, p); } }
            if (kind == 8) for (float y = r.Y + 1; y < r.Bottom; y += 2f) { int hv = MixHash((int)(y * 3), 77); using (var pen = new Pen(Color.FromArgb(8 + (hv & 15), (hv & 16) != 0 ? 255 : 0, (hv & 16) != 0 ? 255 : 0, (hv & 16) != 0 ? 255 : 0), 1f)) g.DrawLine(pen, r.X, y, r.Right, y); }
            if (kind == 10) for (int i = 0; i < Math.Max(12, (int)(r.Width * r.Height / 260)); i++) { int hv = MixHash(i, 4141); float px = r.X + (hv & 0xFF) / 255f * r.Width, py = r.Y + ((hv >> 8) & 0xFF) / 255f * r.Height, tw = phase < 0 ? 0.7f : (float)Math.Abs(Math.Sin(2 * Math.PI * (phase + (hv & 255) / 255f))); using (var br = new SolidBrush(Color.FromArgb((int)(60 + 190 * tw), 255, 255, 235))) g.FillEllipse(br, px, py, 2.2f, 2.2f); }
            if (kind == 11) for (float y = r.Y; y < r.Bottom; y += 4) for (float x = r.X; x < r.Right; x += 4) { bool a = (((int)((x - r.X) / 4)) + ((int)((y - r.Y) / 4))) % 2 == 0; using (var br = new SolidBrush(a ? Color.FromArgb(34, 255, 255, 255) : Color.FromArgb(40, 0, 0, 0))) g.FillRectangle(br, x, y, 4, 4); }
            if (phase >= 0 && MixShines(kind)) MixSheen(g, r, phase, kind == 7 || kind == 1 ? 150 : 110);
        }
        g.Restore(gs);
    }
    void MixSwatch(Graphics g, RectangleF r, Color c, int kind, float rad, Color c2 = default(Color), float phase = -1f)
    {
        if (r.Width < 2 || r.Height < 2) return;
        Func<Color, float, Color> sh = (col, t) => t >= 0 ? MixC(col, Color.White, t) : MixC(col, Color.Black, -t);
        if (kind == MixColours.Glow) { for (int i = 3; i >= 1; i--) using (var p0 = RR(RectangleF.Inflate(r, i * 2.2f, i * 2.2f), rad + i * 2)) using (var br = new SolidBrush(Color.FromArgb(34 - i * 7, c))) g.FillPath(br, p0); }
        int seed = MixHash((int)Math.Round(r.X * 7), (int)Math.Round(r.Y * 13)) ^ c.ToArgb();
        using (var p = RR(r, rad))
        {
            if (kind == MixColours.Translucent) { using (var br0 = new SolidBrush(Color.FromArgb(255, 20, 16, 34))) g.FillPath(br0, p); } else using (var br = new SolidBrush(c)) g.FillPath(br, p);
            var gs = g.Save(); g.SetClip(p, CombineMode.Intersect);
            switch (kind)
            {
                case 16:   // translucent: the colour is a see-through sheet over a checkerboard, so you can see right through it, with a bright top edge
                    { float cs = Math.Max(5f, Math.Min(r.Width, r.Height) / 6f); for (float yy2 = r.Y; yy2 < r.Bottom; yy2 += cs) for (float xx2 = r.X; xx2 < r.Right; xx2 += cs) { bool lt = (((int)((xx2 - r.X) / cs)) + ((int)((yy2 - r.Y) / cs))) % 2 == 0; using (var br = new SolidBrush(lt ? Color.FromArgb(255, 70, 66, 92) : Color.FromArgb(255, 36, 32, 54))) g.FillRectangle(br, xx2, yy2, cs + 0.5f, cs + 0.5f); }
                      using (var br = new LinearGradientBrush(r, Color.FromArgb(205, c), Color.FromArgb(110, c), 90f)) g.FillPath(br, p); using (var br = new LinearGradientBrush(r, Color.FromArgb(120, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillRectangle(br, r.X, r.Y, r.Width, r.Height * 0.45f); using (var pen = new Pen(Color.FromArgb(200, 255, 255, 255), 1.4f)) g.DrawPath(pen, p); }
                    break;
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
                    if (r.Width > r.Height * 2.5f) { var gl = new RectangleF(r.X, r.Y, r.Width, r.Height * 0.46f); using (var br = new LinearGradientBrush(gl, Color.FromArgb(120, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillRectangle(br, gl); var hl = new RectangleF(r.X + r.Width * 0.06f, r.Y + r.Height * 0.08f, r.Width * 0.3f, r.Height * 0.16f); using (var hp = new GraphicsPath()) { hp.AddEllipse(hl); using (var pb = new PathGradientBrush(hp) { CenterColor = Color.FromArgb(150, 255, 255, 255), SurroundColors = new[] { Color.FromArgb(0, 255, 255, 255) } }) g.FillEllipse(pb, hl); } }   // a wide surface (a banner): a wet gloss across the top and a soft hot spot
                    else using (var br = new SolidBrush(Color.FromArgb(120, 255, 255, 255))) g.FillEllipse(br, r.X + r.Width * 0.12f, r.Y + r.Height * 0.07f, r.Width * 0.55f, r.Height * 0.3f);
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
                    for (int i = 0; i < 26; i++) { int hv = MixHash(i, seed); float px = r.X + (hv & 0xFF) / 255f * r.Width, py = r.Y + ((hv >> 8) & 0xFF) / 255f * r.Height, sz = 1.2f + ((hv >> 16) & 3) * 0.7f; int ga = phase < 0 ? 150 + ((hv >> 20) & 63) : (int)(70 + 185 * Math.Abs(Math.Sin(2 * Math.PI * (phase + (hv & 255) / 255f)))); using (var br = new SolidBrush(Color.FromArgb(ga, 255, (hv & 0x400) != 0 ? 255 : 240, (hv & 0x400) != 0 ? 255 : 170))) g.FillEllipse(br, px, py, sz, sz); }
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
            if (kind == 1 || kind == 5 || kind == 7 || kind == 8 || kind == 12) MixBevel(g, p, r, kind == 7 ? 150 : 110);
            if (phase >= 0 && MixShines(kind)) MixSheen(g, r, phase, kind == 7 || kind == 1 ? 150 : 110);
            g.Restore(gs);
            using (var pen = new Pen(Color.FromArgb(kind == 3 ? 70 : 130, 255, 255, 255), 1f)) g.DrawPath(pen, p);
        }
    }
    // a colour code you can copy (click) and paste (right-click puts the code on the clipboard into colour A or B)
    void MixHexClick(Graphics g, RectangleF r, Color c, int side)
    {
        string hex = MixColours.HexOf(c); if (Hot(r)) using (var p = RR(RectangleF.Inflate(r, 3, 2), 7)) using (var pen = new Pen(Color.White, 2f)) g.DrawPath(pen, p);
        var h = Reg(r, () => MixCopyText(hex)); if (side >= 0) { int sd = side; h.OnRight = () => MixPasteHex(sd); }
        Hint2(r, "Click to copy " + hex + (side >= 0 ? ". Right-click to paste a colour code from the clipboard here." : "."));
    }
    internal void MixCopyText(string hex) { try { Clipboard.SetText(hex); mixStatus = hex + " copied."; Toast(mixStatus); } catch { mixStatus = "Could not copy (another program is using the clipboard)."; Toast(mixStatus); } }
    internal void MixPasteHex(int side)
    {
        string t = ""; try { t = (Clipboard.GetText() ?? "").Trim(); } catch { }
        var m = System.Text.RegularExpressions.Regex.Match(t, "^#?([0-9a-fA-F]{6}|[0-9a-fA-F]{3})$"); if (!m.Success) { mixStatus = "The clipboard has no colour code (like #6A1230)."; Toast(mixStatus); return; }
        string h = m.Groups[1].Value; if (h.Length == 3) h = "" + h[0] + h[0] + h[1] + h[1] + h[2] + h[2]; Color c = MixColours.Hex("#" + h.ToUpperInvariant()); MixSet(side, c, 0, "Pasted code"); mixStatus = "Colour " + (side == 0 ? "A" : "B") + " is now #" + h.ToUpperInvariant() + ".";
    }
    void MixText(Graphics g, string s, float px, Color on, RectangleF r, bool bold) { Txt(g, s, px, MixColours.Luma(on) > 0.6 ? Color.FromArgb(255, 20, 12, 36) : Color.FromArgb(255, 250, 244, 255), r, StringAlignment.Center, bold); }
    // a section of the mixer: always a neat box with a neon outline
    void MixBox(Graphics g, RectangleF r) { FillRR(g, TM.C(255, 8, 4, 20), r, 14); Win.DrawNeonBox(g, r, false); }

    // one coloured circle with its label and code (A, B or the merged colour)
    void MixCircle(Graphics g, RectangleF r, Color c, int kind, string title, string name, Color c2 = default(Color), int side = -1)
    {
        var gs = g.Save(); using (var p = new GraphicsPath()) { p.AddEllipse(r); g.SetClip(p, CombineMode.Intersect); }
        MixSwatch(g, r, c, kind, r.Width / 2f, c2, MixWave(r.X, r.Y, 600f)); g.Restore(gs);
        using (var pen = new Pen(Color.FromArgb(230, 255, 255, 255), 2.2f)) g.DrawEllipse(pen, r);
        float fs = Math.Max(10, r.Width * 0.115f); MixText(g, title, fs + 2, c, Rc(r.X, r.Y + r.Height * 0.22f, r.Width, fs + 8), true); MixText(g, MixColours.HexOf(c), fs, c, Rc(r.X, r.Y + r.Height * 0.22f + fs + 8, r.Width, fs + 6), false); MixHexClick(g, Rc(r.X + r.Width * 0.14f, r.Y + r.Height * 0.22f + fs + 8, r.Width * 0.72f, fs + 6), c, side);
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
        // (no letters and no markers on the ring: just the wheel, turning)
        g.SmoothingMode = sm;   // (nothing in the middle either)
    }

    void ShowMixMenu(RectangleF at, int side)
    {
        var m = Win.NewMenu(); int cur = side == 0 ? mixCatA : mixCatB;
        for (int i = 0; i < MixColours.All.Count; i++) { int k = i; var p = MixColours.All[i]; var it = new ToolStripMenuItem(p.Name + (p.Kind == MixColours.Grid ? "  (" + MixColours.ShadeCols * MixColours.ShadeRows + ")" : "  (" + p.Cols.Length + ")")) { Checked = cur == i }; it.Click += (a, b) => { if (side == 0) mixCatA = k; else mixCatB = k; MixChanged(); }; m.Items.Add(it); }
        m.Show(this, new Point((int)at.X, (int)at.Bottom));
    }
    // your name in the colour of your mix (one colour or a fade from A to B); the normal writing when the profile is not following the mix
    void NameText(Graphics g, string s, float px, RectangleF r, Color normal, bool bold) { NameTextC(g, s, px, r, normal, bold, ProfileStore.NameRgb, ProfileStore.NameRgb2); }
    // SAVE AS THEME: the mix becomes a theme of its own in THEMES, PROFILE THEMES (it is not switched on and there is nothing to revert: pick it there when you want it)
    internal string MixSaveAsTheme()
    {
        Color m = MixedNow; double h, s, l; ThemeDef.ToHsl(m, out h, out s, out l); string bg = MixColours.HexOf(ThemeDef.Hsl(h, 0.5, 0.05)).Substring(1);
        string text = "name=Mix " + MixColours.HexOf(m).Substring(1) + "\nviolet=" + MixColours.HexOf(m).Substring(1) + "\ncyan=" + MixColours.HexOf(mixB).Substring(1) + "\npink=" + MixColours.HexOf(mixA).Substring(1) + "\nbg0=" + bg;
        ThemeDef t; string err = Themes.Import(text, "Mix", out t); if (err != null) { Toast(err); return err; } Themes.MarkProfile(t.Name); Prefs.Changed(); mixLastTheme = t.Name;
        mixStatus = "Saved as the theme " + t.Name + ". Open THEMES, then PROFILE THEMES to use it."; Toast(mixStatus); return null;
    }
    // a strip like the scroll bars (solid black, chevrons at both ends) with YOUR LOGO as the handle: it rides along the strip and sets the mix (left all A, right all B); the strip is a box of its own
    void MixLogoBar(Graphics g, RectangleF ob, float v, Action<float> set, Color ca, Color cb, bool enabled)
    {
        FillRR(g, Color.Black, ob, 12); using (var pen = new Pen(Color.FromArgb(enabled ? 190 : 90, NeonViolet), 1.3f)) using (var p = RR(ob, 12)) g.DrawPath(pen, p);
        const float cw = 42; float cy = ob.Y + ob.Height / 2; var lf = Rc(ob.X, ob.Y, cw, ob.Height); var rt = Rc(ob.Right - cw, ob.Y, cw, ob.Height); var inner = Rc(ob.X + cw, ob.Y + 3, ob.Width - 2 * cw, ob.Height - 6);
        DrawChevrons(g, lf.X + cw / 2, cy, 7, true, enabled ? ca : DimC, enabled && Hot(lf)); DrawChevrons(g, rt.X + cw / 2, cy, 7, false, enabled ? cb : DimC, enabled && Hot(rt));
        float lh = Math.Min(16f, ob.Height / 2 - 3), lhw = lh * 1.16f, pad = lhw + 4, trav = Math.Max(1f, inner.Width - 2 * pad), lcx = inner.X + pad + Math.Max(0, Math.Min(1, v)) * trav;
        var track = Rc(inner.X + 4, cy - 5, inner.Width - 8, 10); using (var p = RR(track, 5)) using (var br = new LinearGradientBrush(track, ca, cb, 0f)) { var cbl = new ColorBlend(9); for (int i = 0; i < 9; i++) { cbl.Positions[i] = i / 8f; cbl.Colors[i] = enabled ? MixColours.Blend(ca, cb, i / 8f) : Color.FromArgb(70, 70, 80); } br.InterpolationColors = cbl; g.FillPath(br, p); }
        using (var p = RR(track, 5)) using (var pen = new Pen(Color.FromArgb(120, 255, 255, 255), 1f)) g.DrawPath(pen, p);
        var logoR = Rc(lcx - lhw - 3, cy - lh - 3, lhw * 2 + 6, lh * 2 + 6); bool hotL = enabled && (Hot(logoR) || (dragReg != null && dragReg.R == logoR)); if (enabled) DrawLogo(g, lcx, cy, lh, false, hotL);
        if (!enabled) { Txt(g, "BOTH COLOURS HAVE TO BE ON TO MIX THEM", 10, DimC, ob, StringAlignment.Center, true); return; }
        Action<float> jump = mx => set(Math.Max(0f, Math.Min(1f, (mx - inner.X - pad) / trav)));
        var rg = Reg(inner, () => jump(MousePos.X)); rg.OnDrag = pt => jump(pt.X);
        Reg(lf, () => set(Math.Max(0f, v - 0.05f))); Reg(rt, () => set(Math.Min(1f, v + 0.05f)));
        float downX = 0, downV = 0; var lg = Reg(logoR, () => { downX = MousePos.X; downV = v; }); lg.OnDrag = pt => set(Math.Max(0f, Math.Min(1f, downV + (pt.X - downX) / trav)));
    }
    string mixLastTheme = "";
    // USE AS THEME: the mix becomes a theme and is switched on at once (one press); REVERT in the bar puts the old theme back
    internal void MixUseAsTheme()
    {
        int prev = Themes.Index; string err = MixSaveAsTheme(); if (err != null || mixLastTheme.Length == 0) return; int ti = Themes.IndexOf(mixLastTheme); if (ti < 0) return;
        Themes.Apply(ti); Prefs.Changed(); mixStatus = "Your mix is the theme of the whole program now: " + mixLastTheme + "."; Toast(mixStatus); OfferRevert("Theme " + mixLastTheme + " is on", () => { Themes.Apply(prev); Prefs.Changed(); });
    }
    internal void MixCopyCode() { try { Clipboard.SetText(MixColours.HexOf(MixedNow)); mixStatus = MixColours.HexOf(MixedNow) + " copied."; Toast(mixStatus); } catch { mixStatus = "Could not copy (another program is using the clipboard)."; } }

    // one side of the palette: its own STYLE drop bar and its colours (the left side is colour A, which merges into the colour of the right side, colour B); returns the y under it
    float MixSide(Graphics g, float x, float y0, float w, int side, float vt, float vb)
    {
        Func<float, float, bool> vis = (a, b) => b > vt - 6 && a < vb + 6; int cat = side == 0 ? mixCatA : mixCatB; var pal = MixColours.All[Math.Max(0, Math.Min(MixColours.All.Count - 1, cat))]; Color cur = side == 0 ? mixA : mixB; string curName = side == 0 ? mixNameA : mixNameB;
        float px = x + 14, pw = w - 28, yy = y0 + 12; string hover = ""; float y = y0;
        if (vis(yy, yy + 26)) { Txt(g, side == 0 ? "COLOUR A  -  THE LEFT SIDE" : "COLOUR B  -  THE RIGHT SIDE", 13, Ink, Rc(px, yy, pw - 130, 22), StringAlignment.Near, true); var chip = Rc(px + pw - 120, yy, 120, 22); FillRR(g, cur, chip, 6); StrokeRR(g, Color.FromArgb(200, 255, 255, 255), chip, 6, 1.2f); MixText(g, MixColours.HexOf(cur), 11, cur, chip, true); MixHexClick(g, chip, cur, side); }
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
                float ry = yy + (i / cols) * (cell + gap); if (!vis(ry, ry + cell)) continue; var cr = Rc(px + (i % cols) * (cell + gap), ry, cell, cell); int k = i, sd = side; Color s2 = pal.Cols2 != null ? pal.Cols2[i] : Color.Empty; MixSwatch(g, cr, pal.Cols[i], pal.Kind, 8, s2, MixShines(pal.Kind) ? MixWave(cr.X - px, cr.Y - y, pw * 1.6f) : -1f);
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
        var merged = MixedNow; float dA = Math.Max(72, Math.Min(124, pw * 0.11f)), dM = dA * 1.22f, gapA = Math.Max(50, Math.Min(170, pw * 0.12f)), rowW = dA + gapA + dM + gapA + dA, rowH = dM; var b1 = Rc(px, yy, pw, rowH + 24 + 44 + 44 + 42 + 42 + 12); MixBox(g, b1);
        { float rx = px + (pw - rowW) / 2, ty = yy + 12, cyC = ty + rowH / 2;
          if (vis(ty, ty + rowH))
          {
              MixCircle(g, Rc(rx, cyC - dA / 2, dA, dA), mixA, mixKA, "COLOUR A", mixNameA, mixA2, 0);
              MixArrow(g, Rc(rx + dA + 6, cyC - 27, gapA - 12, 54), mixA, merged, true, !mixAOn ? "OFF" : (MixSolo ? (int)Math.Round((1 - mixTS) * 100) + "%" : (int)Math.Round((1 - mixT) * 100) + "% A"));
              MixCircle(g, Rc(rx + dA + gapA, ty, dM, dM), merged, MixedKind, MixSolo ? "SOLO" : "MERGED", MixSolo ? (mixAOn ? "A ONLY" : "B ONLY") : "A + B", MixedSecond);
              MixArrow(g, Rc(rx + dA + gapA + dM + 6, cyC - 27, gapA - 12, 54), merged, mixB, false, !mixBOn ? "OFF" : (MixSolo ? (int)Math.Round((1 - mixTS) * 100) + "%" : (int)Math.Round(mixT * 100) + "% B"));
              MixCircle(g, Rc(rx + dA + gapA + dM + gapA, cyC - dA / 2, dA, dA), mixB, mixKB, "COLOUR B", mixNameB, mixB2, 1);
          }
          float cy2 = ty + rowH + 12; if (vis(cy2, cy2 + 34))
          {
              var sw = Rc(px + 14, cy2, 88, 32); Btn(g, sw, "SWAP", 0, () => MixSwap(), 12); Hint2(sw, "Swap colour A and colour B");
              var sr = Rc(sw.Right + 14, cy2, 320, 32);   // (the label only: the gradient bar below moves the mix)
              Txt(g, MixSolo ? (mixAOn ? "A ONLY: " : "B ONLY: ") + (int)Math.Round((1 - mixTS) * 100) + "%" : (int)Math.Round((1 - mixT) * 100) + "% A / " + (int)Math.Round(mixT * 100) + "% B", 12, Ink, Rc(sr.X, cy2, 320, 32), StringAlignment.Near, true);
          }
          float cy3 = cy2 + 42; if (vis(cy3, cy3 + 34))
          {
              float cwid = Math.Min(330, (pw - 28 - 10) / 2f); var t1 = Rc(px + 14, cy3, cwid, 32); var t2 = Rc(t1.Right + 10, cy3, cwid, 32);
              Chip(g, t1, "PROFILE COLOUR = MY MIX: " + (mixFollow ? "ON" : "OFF"), mixFollow, () => MixSetFollow(!mixFollow), 1); Hint2(t1, "ON: whatever you change here is your profile colour (banner, rings, accents). OFF: your profile uses the colour of your theme again");
              Chip(g, t2, "FADE MY NAME A TO B: " + (mixFade ? "ON" : "OFF"), mixFade, () => MixSetFade(!mixFade), 1); Hint2(t2, "ON: your name on your profile fades from colour A to colour B (with one colour only it is in that colour). OFF: your name and your bio are in the normal white");
          }
          float cy4 = cy3 + 42; if (vis(cy4, cy4 + 34))
          {
              float qw = Math.Min(210, (pw - 28 - 3 * 10) / 4f); var q1 = Rc(px + 14, cy4, qw, 32); var q2 = Rc(q1.Right + 10, cy4, qw, 32); var q3 = Rc(q2.Right + 10, cy4, qw, 32); var q4 = Rc(q3.Right + 10, cy4, qw, 32);
              Chip(g, q1, "COLOUR A: " + (mixAOn ? "ON" : "OFF"), mixAOn, () => MixSetOn(0, !mixAOn), 1); Hint2(q1, "Switch colour A on or off. With A off the mix is colour B only (one colour). One of the two always stays on");
              Chip(g, q2, "COLOUR B: " + (mixBOn ? "ON" : "OFF"), mixBOn, () => MixSetOn(1, !mixBOn), 1); Hint2(q2, "Switch colour B on or off. With B off the mix is colour A only (one colour). One of the two always stays on");
              Btn(g, q3, "USE A ONLY", mixAOn && !mixBOn ? 4 : 1, () => MixUseOnly(0), 12); Hint2(q3, "Your profile colour is colour A, nothing else");
              Btn(g, q4, "USE B ONLY", mixBOn && !mixAOn ? 4 : 1, () => MixUseOnly(1), 12); Hint2(q4, "Your profile colour is colour B, nothing else");
          }
          float cy5 = cy4 + 42; if (vis(cy5, cy5 + 34))
          {
              bool fan = Edition.Has(Tier.Fantasy); var pt = Rc(px + 14, cy5, Math.Min(560, pw - 28), 32);
              Chip(g, pt, fan ? "STUDIO THEME = MY PROFILE COLOUR: " + (Prefs.ProfileTheme ? "ON" : "OFF") : "STUDIO THEME = MY PROFILE COLOUR  -  FANTASY EDITION", fan && Prefs.ProfileTheme, () => SetProfileTheme(!(fan && Prefs.ProfileTheme)), 1);
              Hint2(pt, fan ? "ON: your profile colour and its finish (metal, chrome, candy...) are the theme of the whole studio. OFF: the studio uses your theme, your profile keeps its colour" : "Fantasy Edition: your profile colour and its finish become the theme of the whole studio. On every edition the colour and its finish are on your profile");
              if (!fan) Txt(g, "Your colour and its finish are on your profile. The whole studio in it is Fantasy Edition.", 11, DimC, Rc(pt.Right + 12, cy5, pw - (pt.Right - px) - 26, 32), StringAlignment.Near, false);
          } }
        yy += b1.Height + 14;
        // ---- the two sides of the palette ----
        bool twoCols = pw >= 760; float colW = twoCols ? (pw - 14) / 2f : pw;
        { float hA = MixSideHeight(colW, mixCatA), hB = MixSideHeight(colW, mixCatB); float rowH2 = twoCols ? Math.Max(hA, hB) : hA;
          var bA = Rc(px, yy, colW, twoCols ? rowH2 : hA); MixBox(g, bA); float endA = MixSide(g, bA.X, bA.Y, bA.Width, 0, vt, vb);
          var bB = twoCols ? Rc(px + colW + 14, yy, colW, rowH2) : Rc(px, yy + hA + 14, colW, hB); MixBox(g, bB); float endB = MixSide(g, bB.X, bB.Y, bB.Width, 1, vt, vb);
          yy = Math.Max(bA.Bottom, bB.Bottom) + 14; }
        // ---- the mixing guide: the gradient and the steps (click or drag them to set the mix) and the wheel (it turns): two boxes ----
        bool two = pw >= 640; float cw2 = two ? (pw - 14) / 2f : pw; float ws = Math.Min(cw2 - 24, 230f); float gh1 = 262f, gh2 = ws + 24f, hMax = Math.Max(gh1, gh2);
        float gt1 = yy, gt2 = two ? yy : yy + gh1 + 14; float gx1 = px, gx2 = two ? px + cw2 + 14 : px; if (two) { gh1 = hMax; gh2 = hMax; }
        MixBox(g, Rc(gx1, gt1, cw2, gh1)); MixBox(g, Rc(gx2, gt2, cw2, gh2));
        { // gradient + steps
            int fk = MixedKind; float by = gt1 + 12, bx = gx1 + 12, bw = cw2 - 24; Txt(g, "HORIZONTAL GRADIENT MIX", 12, DimC, Rc(bx, by, bw, 18), StringAlignment.Near, true); by += 22; var gr = Rc(bx, by, bw, 44); var gtrack = Rc(gr.X, gr.Y + 11, gr.Width, 22);
            if (vis(gr.Y, gr.Bottom))
            {
                using (var p = RR(gtrack, 7)) using (var br = new LinearGradientBrush(gtrack, MixEndA, MixEndB, 0f)) { var cb = new ColorBlend(9); for (int i = 0; i < 9; i++) { cb.Positions[i] = i / 8f; cb.Colors[i] = MixColours.Blend(MixEndA, MixEndB, i / 8f); } br.InterpolationColors = cb; g.FillPath(br, p); }
                MixFinishOver(g, gtrack, 7, fk, MixShines(fk) ? MixPhase() : -1f);
                using (var p = RR(gtrack, 7)) using (var pen = new Pen(Hot(gr) ? Color.White : Color.FromArgb(150, 255, 255, 255), Hot(gr) ? 2.4f : 1f)) g.DrawPath(pen, p);
                float mx = Math.Max(gr.X + 22, Math.Min(gr.Right - 22, gr.X + gr.Width * MixPos)); DrawLogo(g, mx, gr.Y + gr.Height / 2, gr.Height / 2, false, Hot(gr));   // the logo rides over the bar (it is taller than the bar and drawn on top)
                var rg = new HitReg { R = gr }; rg.OnDown = () => MixSetPos((MousePos.X - gr.X) / gr.Width); rg.OnDrag = pt => MixSetPos((pt.X - gr.X) / gr.Width); regs.Add(rg); Hint2(gr, MixSolo ? "Click or drag along the gradient: all the way left keeps the colour as it is, further right fades it darker" : "Click or drag along the gradient to choose how much of colour B is in the mix");
            }
            by += 52; Txt(g, MixColours.HexOf(MixEndA), 10, DimC, Rc(bx, by, 90, 16), StringAlignment.Near, false); MixHexClick(g, Rc(bx, by, 62, 16), MixEndA, mixAOn ? 0 : 1); Txt(g, MixSolo ? "FADE TO DARK" : "A TO B", 10, DimC, Rc(bx + 90, by, bw - 180, 16), StringAlignment.Center, false); Txt(g, MixColours.HexOf(MixEndB), 10, DimC, Rc(bx + bw - 90, by, 90, 16), StringAlignment.Far, false); if (!MixSolo) MixHexClick(g, Rc(bx + bw - 62, by, 62, 16), mixB, 1); by += 28;
            Txt(g, "STEPPED COLOUR BLEND", 12, DimC, Rc(bx, by, bw, 18), StringAlignment.Near, true); by += 24; float sw = (bw - 4 * 8) / 5f; string[] lb = MixSolo ? new[] { "100%", "75%", "50%", "25%", "0%" } : new[] { "100% A", "75% A", "50 / 50", "75% B", "100% B" };
            for (int i = 0; i < 5; i++)
            {
                var sr = Rc(bx + i * (sw + 8), by + 18, sw, 50); float st = i / 4f; Color sc = MixColours.Blend(MixEndA, MixEndB, st); if (!vis(sr.Y, sr.Bottom)) continue; bool hotS = Hot(sr), cur = Math.Abs(MixPos - st) < 0.02f;
                Txt(g, lb[i], 9, cur ? Ink : DimC, Rc(sr.X - 6, by, sr.Width + 12, 16), StringAlignment.Center, cur);
                MixSwatch(g, sr, sc, fk, 7, fk == 5 && !MixSolo ? MixColours.Blend(mixA2.IsEmpty ? mixA : mixA2, mixB2.IsEmpty ? mixB : mixB2, st) : Color.Empty, MixShines(fk) ? MixWave(sr.X - bx, 0, bw) : -1f);
                if (cur) using (var p = RR(RectangleF.Inflate(sr, 3, 3), 9)) using (var pen = new Pen(NeonCyan, 2.4f)) g.DrawPath(pen, p);
                if (hotS) using (var p = RR(RectangleF.Inflate(sr, 2, 2), 9)) using (var pen = new Pen(Color.White, 2.2f)) g.DrawPath(pen, p);
                Txt(g, MixColours.HexOf(sc), 9, DimC, Rc(sr.X - 6, sr.Bottom + 3, sr.Width + 12, 14), StringAlignment.Center, false); MixHexClick(g, Rc(sr.X + 2, sr.Bottom + 3, sr.Width - 4, 14), sc, -1); float tt = st; Reg(sr, () => MixSetPos(tt)); Hint2(sr, "Use " + lb[i] + " as your mix (" + MixColours.HexOf(sc) + ")");
            }
        }
        { // the wheel: no title, no lines, and it turns slowly
            var wr = Rc(gx2 + (cw2 - ws) / 2, gt2 + (gh2 - ws) / 2, ws, ws);
            if (vis(wr.Y, wr.Bottom)) { mixWheelRot = (float)((Environment.TickCount % 60000) / 60000.0 * 360.0 * 4); MixWheel(g, wr); lastInput = DateTime.Now; btnAnim = true; }
            DrawLogo(g, gx2 + cw2 - 38, gt2 + 36, 22, false, false);   // the Purphex logo, for decoration
        }
        yy = two ? gt1 + hMax + 14 : gt2 + gh2 + 14;
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
              var dot = Rc(cr.X + chip / 2 - 9, cr.Y + chip / 2 - 9, 18, 18); using (var br = new SolidBrush(MixColours.Blend(p.A, p.B, p.T))) g.FillEllipse(br, dot); using (var pen = new Pen(Color.White, 1.6f)) g.DrawEllipse(pen, dot);
              using (var pth = RR(cr, 10)) using (var pen = new Pen(Hot(cr) ? Color.White : (i == mixPresetSel ? NeonCyan : Color.FromArgb(150, 255, 255, 255)), Hot(cr) ? 2.6f : (i == mixPresetSel ? 3f : 1f))) g.DrawPath(pen, pth);
              if (Hot(cr)) Hint2(cr, p.Name + "  (" + MixColours.HexOf(p.A) + " to " + MixColours.HexOf(p.B) + ", " + MixColours.HexOf(MixColours.Blend(p.A, p.B, p.T)) + ")");
              Reg(cr, () => MixPresetLoad(ix));
          }
          yy = pb.Bottom + 14; }
        // ---- use it: a theme, or the code ----
        { var ub = Rc(px, yy, pw, 12 + 38 + 12 + 26); MixBox(g, ub); float uy = ub.Y + 12;
          if (vis(uy, uy + 40)) { float bw = Math.Min(230, (pw - 28 - 20) / 3f); var u1 = Rc(ub.X + 14, uy, bw, 38); var u1b = Rc(u1.Right + 10, uy, bw, 38); var u2 = Rc(u1b.Right + 10, uy, Math.Max(bw, Math.Min(380, pw - 28 - 2 * bw - 20)), 38);
              Btn(g, u1b, "USE AS THEME", 2, () => MixUseAsTheme(), 12); Hint2(u1b, "Make your mix the theme of the whole program right now (one press). REVERT puts the old theme back");
              Btn(g, u1, "SAVE AS THEME", 1, () => MixSaveAsTheme(), 12); Hint2(u1, "Save your mix as a theme for the whole program (the merged colour, A and B). It goes in THEMES, PROFILE THEMES, where you can use it any time or delete it");
              Btn(g, u2, "COPY PROFILE THEME HEX CODE", 1, () => MixCopyCode(), 12); Hint2(u2, "Copy the code of the merged colour (like #A79CC4)"); }
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
