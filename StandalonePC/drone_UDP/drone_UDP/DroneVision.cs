using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV.Cuda;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.UI;
using Emgu.CV.Util;
using Emgu.CV;
using Emgu.CV.Superres;

namespace BebopSharp
{
    public static class StreamBuffer
    {
        public static Capture Stream { get; set; }
        public static Mat CurrentFrame { get; set; } = null;
        public static bool IsRunning { get; set; } = false;
        public static Size FrameSize { get; set; }
        public static int Blur { get; set; }

        public static Mat GetCurrentFrame()
        {
            if (CurrentFrame != null)
            {
                Mat frame = new Mat();
                CurrentFrame.CopyTo(frame);
                return frame;
            }

            return null;
        }

        public static void Run()
        {
            Stream.ImageGrabbed += StreamOnImageGrabbed;
            var b = Stream.QueryFrame();
            Stream.Start();
        }

        private static void StreamOnImageGrabbed(object sender, EventArgs e)
        {
            var frame = Stream.QueryFrame();

        }
    }
}
