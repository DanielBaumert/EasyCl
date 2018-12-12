#region MIT License

// Copyright (c) 2018-2022 EasyCl - Daniel Baumert
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

#endregion   

using OpenCL.Net;
using System;
using System.Linq;
using System.Drawing;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using SystemGDI = System.Drawing.Imaging;

namespace EasyCL {
    public static class EasyClCompiler {
        private static bool _hasDevice;
        public static ref readonly bool HasDevice => ref _hasDevice;

        private static MemFlags _memFlags;
        private static ImageFormat _imageFormat;
        private static IntPtr[] _originPtr;

        public static ref readonly MemFlags MemFlags => ref _memFlags;
        public static ref readonly ImageFormat ImageFormat => ref _imageFormat;
        public static ref readonly IntPtr[] OriginPtr => ref _originPtr;

        private static Device _device;
        private static Context _context;
        private static CommandQueue _commandQueue;

        public static ref readonly Device Device => ref _device;
        public static ref readonly Context Context => ref _context;
        public static ref readonly CommandQueue CommandQueue => ref _commandQueue;

        /// <summary>
        /// Laden der GPU und Eigenschaften
        /// </summary>
        static EasyClCompiler() {
            _hasDevice = false;

            Platform[] platforms = Cl.GetPlatformIDs(out ErrorCode error);
            if (error != ErrorCode.Success) {
                throw new GPUException("Paltform", error.ToString());
            }

            List<Device> devices = new List<Device>();

            if (platforms.Length > 0) {
                foreach (Platform platform in platforms) {
                    foreach (Device device in Cl.GetDeviceIDs(platform, DeviceType.Gpu, out error)) {
                        if (error != ErrorCode.Success) {
                            throw new GPUException("Device", error.ToString());
                        }
                        devices.Add(device);
                    }
                }
            }

            if (devices.Count > 0) {
                foreach (Device device in devices) {
                    if (Cl.GetDeviceInfo(device, DeviceInfo.ImageSupport, out error).CastTo<bool>()) {
                        Context context = Cl.CreateContext(null, 1, new[] { device }, null, IntPtr.Zero, out error);
                        if (error != ErrorCode.Success) {
                            throw new GPUException("Init", error.ToString());
                        }
                        _device = device;
                        _context = context;
                        _commandQueue = Cl.CreateCommandQueue(_context, _device, CommandQueueProperties.None, out _);
                        _hasDevice = true;
                    }
                }
            }

            #region Init Basic

            _memFlags = MemFlags.CopyHostPtr;
            _imageFormat = new ImageFormat(ChannelOrder.BGRA, ChannelType.Unsigned_Int8);
            _originPtr = new[] { (IntPtr)0, (IntPtr)0, (IntPtr)0 };

            #endregion Init Basic
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="sourceGpuCode">CL code to execute</param>
        /// <param name="methodeName">main methode name (__kernal xxxxx())</param>
        /// <returns></returns>
        public static OpenClBridge CompileProgramFromSource(string sourceGpuCode, string methodeName) {
            #region create program

            Program program = Cl.CreateProgramWithSource(Context, 1, new[] { sourceGpuCode }, null, out ErrorCode error);
            if (error != ErrorCode.Success) {
                throw new GPUException("Compile0x1", Cl.GetProgramBuildInfo(program, Device, ProgramBuildInfo.Log, out error).ToString());
            }
            if (Cl.BuildProgram(program, 1, new[] { Device }, string.Empty, null, IntPtr.Zero) != ErrorCode.Success) {
                throw new GPUException("Compile0x2", Cl.GetProgramBuildInfo(program, Device, ProgramBuildInfo.Log, out error).ToString());
            }
            if (Cl.GetProgramBuildInfo(program, Device, ProgramBuildInfo.Status, out error).CastTo<BuildStatus>() != BuildStatus.Success) {
                throw new GPUException("Compile0x3", Cl.GetProgramBuildInfo(program, Device, ProgramBuildInfo.Log, out error).ToString());
            }

            #endregion create program

            #region create kernal

            Kernel kernel = Cl.CreateKernel(program, methodeName, out error);
            if (error != ErrorCode.Success) {
                throw new GPUException("Compile0x4", error.ToString());
            }
            return new OpenClBridge(kernel, sourceGpuCode, methodeName);

            #endregion create kernal
        }

        /// <summary>
        /// Convert GID pixelformat to GPU pixelformat
        /// </summary>
        /// <param name="format"></param>
        /// <returns></returns>
        public static ImageFormat ToGPU(this SystemGDI.PixelFormat format) {
            switch (format) {
                case SystemGDI.PixelFormat.Format32bppArgb:
                case SystemGDI.PixelFormat.Format32bppPArgb:
                case SystemGDI.PixelFormat.Format32bppRgb:
                    return new ImageFormat(ChannelOrder.RGBA, ChannelType.Unsigned_Int8);

                case SystemGDI.PixelFormat.Format24bppRgb:
                    return new ImageFormat(ChannelOrder.RGB, ChannelType.Unsigned_Int8);

                case SystemGDI.PixelFormat.Format16bppArgb1555:
                    return new ImageFormat(ChannelOrder.ARGB, ChannelType.Unorm_Short555);

                default:
                    throw new NotSupportedException();
            }
        }


        public static Program Compile(string source) {
            Program program = Cl.CreateProgramWithSource(Context, 1, new[] { source }, null, out ErrorCode error);

            if (error != ErrorCode.Success) {
                throw new GPUException("Compile0x1", Cl.GetProgramBuildInfo(program, Device, ProgramBuildInfo.Log, out error).ToString());
            }
            if (Cl.BuildProgram(program, 1, new[] { Device }, string.Empty, null, IntPtr.Zero) != ErrorCode.Success) {
                throw new GPUException("Compile0x2", Cl.GetProgramBuildInfo(program, Device, ProgramBuildInfo.Log, out error).ToString());
            }
            if (Cl.GetProgramBuildInfo(program, Device, ProgramBuildInfo.Status, out error).CastTo<BuildStatus>() != BuildStatus.Success) {
                throw new GPUException("Compile0x3", Cl.GetProgramBuildInfo(program, Device, ProgramBuildInfo.Log, out error).ToString());
            }

            return program;
        }


        public static Kernel CreateProgram(ref Program program, string methodeName) {
            Kernel kernel = Cl.CreateKernel(program, methodeName, out ErrorCode error);
            if (error != ErrorCode.Success) {
                throw new GPUException("Compile0x4", error.ToString());
            }
            return kernel;
        }

    }

    [Serializable]
    public class GPUException : Exception {

        public GPUException(string name, string errorStrg, [CallerFilePath] string path = "", [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string methode = "")
            : this($"ERROR:[{path}:{methode}({lineNumber})] {name} ({errorStrg})") {
        }

        public GPUException([CallerFilePath] string path = "", [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string methode = "")
            : this($"ERROR:[{path}:{methode}({lineNumber})]") {
        }

        public GPUException(string message) : base(message) {
        }
    }

    /// <summary>
    /// Hold bitmap statistics for the GPU
    /// </summary>
    public class KernelImage : IDisposable {
        public int ParameterIndex { get; private set; }
        public ImageFormat ImageFormat { get; private set; }

        /// <summary>
        /// Image width in px
        /// </summary>
        public int Width { get; private set; }

        /// <summary>
        /// Image height in px
        /// </summary>
        public int Height { get; private set; }

        /// <summary>
        /// raw image format
        /// </summary>
        public byte[] Source;

        public IMem GpuBuffer;

        public MemFlags Flags { get; private set; }
        public IntPtr[] WorkGroupSizePtr { get; private set; }
        public IntPtr[] RegionPtr { get; private set; }

        private KernelImage(int index, MemFlags flags, int width, int height, SystemGDI.PixelFormat format)
            : this(index, flags, width, height, format.ToGPU()) {
        }

        public KernelImage(int index, byte[] bytes, int width, int height, SystemGDI.PixelFormat pixelFormat)
            : this(index, MemFlags.CopyHostPtr, bytes, width, height, pixelFormat.ToGPU()) {
        }

        public KernelImage(int index, MemFlags flags, byte[] bytes, int width, int height, SystemGDI.PixelFormat pixelFormat)
             : this(index, flags, bytes, width, height, pixelFormat.ToGPU()) { }

        public KernelImage(int index, MemFlags flags, byte[] bytes, int width, int height, ImageFormat pixelFormat)
             : this(index, flags, width, height, pixelFormat) {
            Source = bytes;
            GpuBuffer = Cl.CreateImage2D(EasyClCompiler.Context, (OpenCL.Net.MemFlags)Flags, ImageFormat, (IntPtr)Width, (IntPtr)Height, (IntPtr)0, Source, out ErrorCode error);
            if (error != ErrorCode.Success) {
                throw new GPUException("SetImage2D", error.ToString());
            }
        }

        public KernelImage(int index, MemFlags flags, Bitmap bmp) : this(index, flags, bmp.Width, bmp.Height, bmp.PixelFormat) {
            Rectangle bmpRect = new Rectangle(Point.Empty, bmp.Size);
            SystemGDI.BitmapData bmpData = bmp.LockBits(bmpRect, SystemGDI.ImageLockMode.ReadWrite, bmp.PixelFormat);
            IntPtr bmpPtr = bmpData.Scan0;
            int byteCount = bmpData.Stride * Height;
            Source = new byte[byteCount];
            Marshal.Copy(bmpPtr, Source, 0, byteCount);
            GpuBuffer = Cl.CreateImage2D(EasyClCompiler.Context, (OpenCL.Net.MemFlags)flags, ImageFormat, (IntPtr)Width, (IntPtr)Height, (IntPtr)0, Source, out ErrorCode error);
            if (error != ErrorCode.Success) {
                throw new GPUException("SetImage2D", error.ToString());
            }
            bmp.UnlockBits(bmpData);
        }

        private KernelImage(int index, MemFlags flags, int width, int height, ImageFormat format) {
            ParameterIndex = index;
            Flags = (MemFlags)flags;
            ImageFormat = format;
            Width = width;
            Height = height;

            RegionPtr = new[] { (IntPtr)Width, (IntPtr)Height, (IntPtr)1 };
            WorkGroupSizePtr = new[] { (IntPtr)Width, (IntPtr)Height, (IntPtr)1 };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSource(in byte[] source) {
            if (source.Length != Source.Length) {
                throw new ArithmeticException();
            }

            Interlocked.Exchange(ref Source, source);

            Cl.ReleaseMemObject(GpuBuffer);

            GpuBuffer.Dispose();

            Interlocked.Exchange(
                ref GpuBuffer,
                Cl.CreateImage2D(EasyClCompiler.Context, (OpenCL.Net.MemFlags)Flags, ImageFormat, (IntPtr)Width, (IntPtr)Height, (IntPtr)0, Source, out ErrorCode error))
                ;

            if (error != ErrorCode.Success) {
                throw new GPUException("SetImage2D", error.ToString());
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSource(Bitmap bmp) {
            Rectangle bmpRect = new Rectangle(Point.Empty, bmp.Size);
            SystemGDI.BitmapData bmpData = bmp.LockBits(bmpRect, SystemGDI.ImageLockMode.ReadWrite, bmp.PixelFormat);
            IntPtr bmpPtr = bmpData.Scan0;
            SetSource(bmpPtr);
            bmp.UnlockBits(bmpData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSource(IntPtr sourcePtr) {
            Cl.ReleaseMemObject(GpuBuffer);
            GC.SuppressFinalize(GpuBuffer);
            Marshal.Copy(sourcePtr, Source, 0, Source.Length);
            GpuBuffer = Cl.CreateImage2D(EasyClCompiler.Context, (OpenCL.Net.MemFlags)Flags, ImageFormat, (IntPtr)Width, (IntPtr)Height, (IntPtr)0, Source, out _);
        }

        public void Dispose() {
            Cl.ReleaseMemObject(GpuBuffer);
            GC.SuppressFinalize(GpuBuffer);
            GC.SuppressFinalize(this);
        }
    }



    /// <summary>
    /// Hold the GPU program ready to run
    /// </summary>
    public struct OpenClBridge {
        public Kernel Kernel { get; private set; }
        public string SourceCode { get; set; }
        public string MethodeName { get; set; }

        public OpenClBridge(Kernel kernel, string sourcCode, string methodeName) {
            Kernel = kernel;
            SourceCode = sourcCode;
            MethodeName = methodeName;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetKernelArg<T>(int index, IList<T> items) where T : struct
            => SetKernelArg(index, items.ToArray(), MemFlags.CopyHostPtr);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetKernelArg<T>(int index, T[] items) where T : struct
            => SetKernelArg(index, items, MemFlags.CopyHostPtr);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetKernelArg<T>(int index, IList<T> items, MemFlags flags) where T : struct
            => SetKernelArg(index, items.ToArray(), flags);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetKernelArg<T>(int index, T item) where T : struct
            => Cl.SetKernelArg(Kernel, (uint)index, item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetKernelArg<T>(int index, T[] items, MemFlags flags) where T : struct {
            IMem<T> memBuffer = Cl.CreateBuffer(EasyClCompiler.Context, (OpenCL.Net.MemFlags)flags, items, out _);
            Cl.SetKernelArg(Kernel, (uint)index, memBuffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetKernerArgImg2D(KernelImage image) {
            if (Cl.SetKernelArg(Kernel, (uint)image.ParameterIndex, (IntPtr)IntPtr.Size, image.GpuBuffer) != ErrorCode.Success) {
                throw new GPUException("Cl.SetKernelArg - SetKernerArgImg2D");
            }
            Cl.EnqueueWriteImage(EasyClCompiler.CommandQueue, image.GpuBuffer, Bool.True, EasyClCompiler.OriginPtr, image.RegionPtr, (IntPtr)0, (IntPtr)0, image.Source, 0, null, out _);
        }

        /// <summary>
        /// Runing the GPU program
        /// </summary>
        /// <param name="workGroupSizePtr">image dimention in 3D (z = 1)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Execute(IntPtr[] workGroupSizePtr) {
            ErrorCode error = Cl.EnqueueNDRangeKernel(EasyClCompiler.CommandQueue, Kernel, 2, null, workGroupSizePtr, null, 0, null, out _);
            error = Cl.Finish(EasyClCompiler.CommandQueue);
        }

        /// <summary>
        /// Write back an existing parameter image
        /// </summary>
        /// <param name="image">Write back image</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetKernerImg2D(KernelImage image)
            => Cl.EnqueueReadImage(EasyClCompiler.CommandQueue, image.GpuBuffer, Bool.True, EasyClCompiler.OriginPtr, image.RegionPtr, (IntPtr)0, (IntPtr)0, image.Source, 0, null, out _);

        public void UpdateKernel(ref Kernel kernel) {
            Kernel.Dispose();
            Kernel = kernel;
        }
    }

    [Flags]
    public enum MemFlags : ulong {
        None = 0,
        ReadWrite = 1,
        WriteOnly = 2,
        ReadOnly = 4,
        UseHostPtr = 8,
        AllocHostPtr = 16,
        CopyHostPtr = 32
    }
}
