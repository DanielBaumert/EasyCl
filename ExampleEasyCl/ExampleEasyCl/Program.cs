using EasyCL;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ExampleEasyCl {
    class Program {


        static void Main(string[] args) {

            string gpuCode = Encoding.Default.GetString(Properties.Resources.GrayScale.Skip(3).ToArray());
            OpenClBridge bridge = EasyClCompiler.CompileProgramFromSource(gpuCode, "run");

            using (Bitmap bmp = Properties.Resources.bild1) {

                Rectangle rect = new Rectangle(Point.Empty, bmp.Size);
                BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.ReadWrite, bmp.PixelFormat);
                int byteCount = bmpData.Stride * bmp.Height;
                byte[] buffer = new byte[byteCount];
                unsafe {
                    fixed (byte* bufferPtr = buffer) {
                        CopyMemory((IntPtr)bufferPtr, bmpData.Scan0, byteCount);
                    }
                }

                using (KernelImage kernelImageIn = new KernelImage(0, MemFlags.CopyHostPtr, buffer, bmp.Width, bmp.Height, bmp.PixelFormat)) {
                    using (KernelImage kernelImageOut = new KernelImage(1, MemFlags.CopyHostPtr, bmp)) {
                        bridge.SetKernerArgImg2D(kernelImageIn);
                        bridge.SetKernerArgImg2D(kernelImageOut);
                        bridge.Execute(kernelImageIn.WorkGroupSizePtr);
                        unsafe {
                            fixed (byte* outBuffPtr = kernelImageOut.Source) {
                                MoveMemory(bmpData.Scan0, (IntPtr)outBuffPtr, byteCount);
                            }
                        }
                    }
                }

                bmp.UnlockBits(bmpData);
                bmp.Save("image-green-scale.png", ImageFormat.Png);
            }
        }

        [DllImport("kernel32.dll", SetLastError = false)]
        private static extern void CopyMemory(IntPtr dest, IntPtr src, int count);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern void MoveMemory(IntPtr dest, IntPtr src, int count);
    }

}
