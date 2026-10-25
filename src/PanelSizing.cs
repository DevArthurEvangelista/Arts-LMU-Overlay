using System;
using System.Drawing;

namespace Arthur.LmuOverlay
{
    public static class PanelSizing
    {
        public const int Left=1,Right=2,Top=4,Bottom=8;
        public static Size Minimum(Size original)
        { return new Size(Math.Max(128,(int)(original.Width*.35)),Math.Max(80,(int)(original.Height*.35))); }
        public static int HitEdges(Point point,Size size,int grip=16)
        {
            return (point.X<grip ? Left : point.X>=size.Width-grip ? Right : 0)|
                (point.Y<grip ? Top : point.Y>=size.Height-grip ? Bottom : 0);
        }
        public static Rectangle Resize(Rectangle start,Point delta,int edges,Size minimum,Rectangle area)
        {
            int width=start.Width+((edges&Right)!=0 ? delta.X : (edges&Left)!=0 ? -delta.X : 0);
            int height=start.Height+((edges&Bottom)!=0 ? delta.Y : (edges&Top)!=0 ? -delta.Y : 0);
            int maxWidth=(edges&Left)!=0 ? start.Right-area.Left : area.Right-start.Left;
            int maxHeight=(edges&Top)!=0 ? start.Bottom-area.Top : area.Bottom-start.Top;
            var size=Fit(width,height,minimum.Width,minimum.Height,maxWidth,maxHeight);
            return new Rectangle((edges&Left)!=0 ? start.Right-size.Width : start.Left,
                (edges&Top)!=0 ? start.Bottom-size.Height : start.Top,size.Width,size.Height);
        }
        public static Size Fit(int width,int height,int minimumWidth,int minimumHeight,int maximumWidth,int maximumHeight)
        {
            maximumWidth=Math.Max(1,maximumWidth);maximumHeight=Math.Max(1,maximumHeight);
            return new Size(Math.Max(Math.Min(minimumWidth,maximumWidth),Math.Min(maximumWidth,width)),
                Math.Max(Math.Min(minimumHeight,maximumHeight),Math.Min(maximumHeight,height)));
        }
    }
}
