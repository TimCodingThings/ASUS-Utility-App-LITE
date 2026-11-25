using System;
using System.Drawing;
namespace ASUSUtilityLite {
internal static class WindowPlacement {
    // Keep physical desktop coordinates, including negative monitor positions.
    // Prefer the screen with the greatest overlap, or the nearest screen when
    // the saved monitor no longer exists. Oversized windows keep the title visible.
    internal static Point Restore(Point saved,Size window,Rectangle[] areas) {
        if(areas==null||areas.Length==0)return Point.Empty;
        Rectangle chosen=areas[0];long bestOverlap=-1;double bestDistance=double.MaxValue;
        foreach(Rectangle area in areas) {
            long width=Math.Max(0,Math.Min((long)saved.X+window.Width,area.Right)-Math.Max((long)saved.X,area.Left));
            long height=Math.Max(0,Math.Min((long)saved.Y+window.Height,area.Bottom)-Math.Max((long)saved.Y,area.Top));
            long overlap=width*height;
            double dx=(double)saved.X-Math.Max(area.Left,Math.Min(area.Right,saved.X));
            double dy=(double)saved.Y-Math.Max(area.Top,Math.Min(area.Bottom,saved.Y));
            double distance=dx*dx+dy*dy;
            if(overlap>bestOverlap||(overlap==bestOverlap&&distance<bestDistance)) {
                chosen=area;bestOverlap=overlap;bestDistance=distance;
            }
        }
        return new Point(Math.Max(chosen.Left,Math.Min(chosen.Left+Math.Max(0,chosen.Width-window.Width),saved.X)),
            Math.Max(chosen.Top,Math.Min(chosen.Top+Math.Max(0,chosen.Height-window.Height),saved.Y)));
    }
}
}
