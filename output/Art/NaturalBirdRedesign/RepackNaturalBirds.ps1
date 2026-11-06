param(
    [Parameter(Mandatory=$true)][string]$EnemyName,
    [Parameter(Mandatory=$true)][string]$GeneratedPath,
    [Parameter(Mandatory=$true)][string]$OutputPath
)

Add-Type -AssemblyName System.Drawing
if (-not ('EnemySpritePacker' -as [type])) {
    Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text.RegularExpressions;

public static class EnemySpritePacker {
    private sealed class Frame { public Rectangle Bounds; public int Id; public int Count; }
    private static void LimitPalette(Bitmap bitmap, Bitmap editable, string output) {
        var histogram=new Dictionary<int,int>();
        for(int y=0;y<bitmap.Height;y++) for(int x=0;x<bitmap.Width;x++) {
            Color c=bitmap.GetPixel(x,y);
            if(c.A==0) continue;
            int key=(c.R<<16)|(c.G<<8)|c.B;
            if(!histogram.ContainsKey(key)) histogram[key]=0;
            histogram[key]++;
        }
        // Keep small species-defining accents (blue neck, red wattle, orange eyes)
        // instead of letting a large dark body consume the entire color budget.
        string name=System.IO.Path.GetFileName(output);
        int[] colors=name.StartsWith("Spitter")
            ? new[]{0x101321,0x151A29,0x2B3245,0x155AD4,0x4182DF,0xB93640,0x837154,0xE3BD39}
            : name.StartsWith("ChargingWarden")
                ? new[]{0x251F24,0x4B342C,0x805336,0xB08755,0xDFC28B,0xB99E70,0xE69B2E,0x68625B}
                : new[]{0x0C1122,0x25324E,0x49607F,0x8395A4,0xE5D8AB,0xB1A17B,0xD9A81F,0xF8EFCE};
        var palette=colors.Select(c=>Color.FromArgb(255,(c>>16)&255,(c>>8)&255,c&255)).ToArray();
        var mapping=new Dictionary<int,Color>();
        foreach(int key in histogram.Keys) {
            int r=(key>>16)&255,g=(key>>8)&255,b=key&255;
            mapping[key]=palette.OrderBy(c=>(c.R-r)*(c.R-r)+(c.G-g)*(c.G-g)+(c.B-b)*(c.B-b)).First();
        }
        foreach(var target in new[]{bitmap,editable}) for(int y=0;y<target.Height;y++) for(int x=0;x<target.Width;x++) {
            Color c=target.GetPixel(x,y);
            if(c.A>0) target.SetPixel(x,y,mapping[(c.R<<16)|(c.G<<8)|c.B]);
        }
        Console.WriteLine("Shared animation palette: "+palette.Length+" opaque colors, no dithering.");
    }
    public static void Pack(string input, string metadata, string output) {
        var frames = Regex.Matches(metadata, @"      name: (\S+)\r?\n      rect:\r?\n        serializedVersion: 2\r?\n        x: (\d+)\r?\n        y: (\d+)\r?\n        width: (\d+)\r?\n        height: (\d+)");
        if (frames.Count != 12) throw new Exception("Expected exactly twelve original slices.");
        var rects = new Rectangle[12];
        for (int i=0; i<12; i++) {
            var m=frames[i];
            int x=int.Parse(m.Groups[2].Value), y=int.Parse(m.Groups[3].Value);
            int w=int.Parse(m.Groups[4].Value), h=int.Parse(m.Groups[5].Value);
            rects[i]=new Rectangle(x,1086-y-h,w,h);
        }
        using (var source = new Bitmap(input))
        using (var result = new Bitmap(1448,1086,PixelFormat.Format32bppArgb))
        using (var editable = new Bitmap(192,144,PixelFormat.Format32bppArgb)) {
            int sw=source.Width, sh=source.Height;
            var pixels=new Color[sw*sh];
            var labels=new int[sw*sh];
            for(int y=0;y<sh;y++) for(int x=0;x<sw;x++) pixels[y*sw+x]=source.GetPixel(x,y);
            var queue=new int[sw*sh];
            var components=new List<Frame>();
            int id=0;
            for(int seed=0;seed<pixels.Length;seed++) {
                if(labels[seed]!=0 || pixels[seed].A<128) continue;
                id++; int head=0,tail=0;
                queue[tail++]=seed; labels[seed]=id;
                int minX=sw,minY=sh,maxX=0,maxY=0;
                while(head<tail) {
                    int current=queue[head++], x=current%sw, y=current/sw;
                    minX=Math.Min(minX,x); maxX=Math.Max(maxX,x);
                    minY=Math.Min(minY,y); maxY=Math.Max(maxY,y);
                    for(int dy=-1;dy<=1;dy++) for(int dx=-1;dx<=1;dx++) {
                        int nx=x+dx,ny=y+dy;
                        if(nx<0 || nx>=sw || ny<0 || ny>=sh) continue;
                        int ni=ny*sw+nx;
                        if(labels[ni]!=0 || pixels[ni].A<128) continue;
                        labels[ni]=id; queue[tail++]=ni;
                    }
                }
                if(tail>1000) components.Add(new Frame {Id=id,Count=tail,Bounds=Rectangle.FromLTRB(minX,minY,maxX+1,maxY+1)});
            }
            if(components.Count!=12) throw new Exception("Expected twelve separate character silhouettes; found "+components.Count);
            var ordered=components.OrderBy(f=>(int)((f.Bounds.Top+f.Bounds.Height/2f)/(sh/3f)))
                .ThenBy(f=>f.Bounds.Left+f.Bounds.Width/2f).ToArray();
            for (int i=0; i<12; i++) {
                var frame=ordered[i];
                int minX=frame.Bounds.Left,minY=frame.Bounds.Top;
                int srcW=frame.Bounds.Width, srcH=frame.Bounds.Height;
                double factor=46.0/Math.Max(srcW,srcH);
                int lowW=Math.Max(1,(int)Math.Round(srcW*factor));
                int lowH=Math.Max(1,(int)Math.Round(srcH*factor));
                for(int y=0;y<lowH;y++) for(int x=0;x<lowW;x++) {
                    int sampleX=minX+Math.Min(srcW-1,(int)((x+0.5)*srcW/lowW));
                    int sampleY=minY+Math.Min(srcH-1,(int)((y+0.5)*srcH/lowH));
                    Color color=pixels[sampleY*sw+sampleX];
                    if(labels[sampleY*sw+sampleX]==frame.Id)
                        editable.SetPixel(i%4*48+(48-lowW)/2+x,i/4*48+47-lowH+y,Color.FromArgb(255,color.R,color.G,color.B));
                }
                var safe=rects[i];
                // Existing source rectangles overlap slightly. Keep every new frame
                // outside its neighbors' slices so old animation references stay safe.
                foreach(var other in rects) {
                    if(other==rects[i] || !safe.IntersectsWith(other)) continue;
                    float dx=(other.Left+other.Width/2f)-(rects[i].Left+rects[i].Width/2f);
                    float dy=(other.Top+other.Height/2f)-(rects[i].Top+rects[i].Height/2f);
                    if(Math.Abs(dx)>Math.Abs(dy)) {
                        if(dx<0) safe=Rectangle.FromLTRB(Math.Max(safe.Left,other.Right),safe.Top,safe.Right,safe.Bottom);
                        else safe=Rectangle.FromLTRB(safe.Left,safe.Top,Math.Min(safe.Right,other.Left),safe.Bottom);
                    } else {
                        if(dy<0) safe=Rectangle.FromLTRB(safe.Left,Math.Max(safe.Top,other.Bottom),safe.Right,safe.Bottom);
                        else safe=Rectangle.FromLTRB(safe.Left,safe.Top,safe.Right,Math.Min(safe.Bottom,other.Top));
                    }
                }
                safe.Inflate(-2,-2);
                if(safe.Width<20 || safe.Height<20) throw new Exception("No safe packing area for frame "+i);
                double upscale=Math.Min((double)safe.Width/lowW,(double)safe.Height/lowH);
                int drawW=(int)Math.Floor(lowW*upscale), drawH=(int)Math.Floor(lowH*upscale);
                int destX=safe.Left+(safe.Width-drawW)/2, destY=safe.Bottom-drawH;
                for(int y=0;y<drawH;y++) for(int x=0;x<drawW;x++) {
                    int logicalX=Math.Min(lowW-1,x*lowW/drawW), logicalY=Math.Min(lowH-1,y*lowH/drawH);
                    int sampleX=minX+Math.Min(srcW-1,(int)((logicalX+0.5)*srcW/lowW));
                    int sampleY=minY+Math.Min(srcH-1,(int)((logicalY+0.5)*srcH/lowH));
                    Color color=pixels[sampleY*sw+sampleX];
                    if(labels[sampleY*sw+sampleX]==frame.Id) result.SetPixel(destX+x,destY+y,Color.FromArgb(255,color.R,color.G,color.B));
                }
                Console.WriteLine(frames[i].Groups[1].Value+": "+lowW+"x"+lowH+" logical pixels; original slice preserved");
            }
            LimitPalette(result,editable,output);
            result.Save(output,ImageFormat.Png);
            editable.Save(output.Replace("_coarse.png","_48px.png"),ImageFormat.Png);
        }
    }
}
'@
}
$metadataPath = Join-Path (Get-Location) ('Assets/Sprites/Characters/' + $EnemyName + '/' + $EnemyName + '_animations.png.meta')
[EnemySpritePacker]::Pack($GeneratedPath, [System.IO.File]::ReadAllText($metadataPath), $OutputPath)
