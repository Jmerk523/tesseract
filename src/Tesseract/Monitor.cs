using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Tesseract
{
    public class MonitorProgressEventArgs : EventArgs
    {
        public int Left { get; }
        public int Right { get; }
        public int Top { get; }
        public int Bottom { get; }
        public int Progress { get; }

        public MonitorProgressEventArgs(int left, int right, int top, int bottom, int progress)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
            Progress = progress;
        }
    }

    public class MonitorCancelEventArgs : EventArgs
    {
        public bool Cancel { get; set; }
        public int Words { get; }
        public int Progress { get; }

        public MonitorCancelEventArgs(int words, int progress)
        {
            Words = words;
            Progress = progress;
        }
    }

    /// <summary>
    /// Progress monitor and cancellation
    /// </summary>
    public class Monitor : DisposableBase
    {
        private readonly HandleRef _handleRef;

        // References to delegates to prevent garbage collection while referenced from a native object
        private readonly Interop.ProgressFunc onProgressDelegate;
        private readonly Interop.CancelFunc onCancelCheckDelegate;

        // Don't call the SetFunc APIs until the first event subscriber registers,
        // to avoid unnecessary calls from native to managed code if there are no subscribers
        private event EventHandler<MonitorProgressEventArgs> progressEvent;
        private event EventHandler<MonitorCancelEventArgs> cancelCheckEvent;

        public event EventHandler<MonitorProgressEventArgs> Progress
        {
            add
            {
                if (!IsDisposed && progressEvent == null && _handleRef.Handle != IntPtr.Zero)
                {
                    Interop.TessApi.Native.MonitorSetProgressFunc(_handleRef, onProgressDelegate);
                }
                progressEvent += value;
            }
            remove
            {
                progressEvent -= value;
            }
        }

        public event EventHandler<MonitorCancelEventArgs> CancelCheck
        {
            add
            {
                if (!IsDisposed && cancelCheckEvent == null && _handleRef.Handle != IntPtr.Zero)
                {
                    Interop.TessApi.Native.MonitorSetCancelFunc(_handleRef, onCancelCheckDelegate);
                }
                cancelCheckEvent += value;
            }
            remove
            {
                cancelCheckEvent -= value;
            }
        }

        internal HandleRef Handle
        {
            get
            {
                return _handleRef;
            }
        }

        internal Monitor()
        {
            this._handleRef = new HandleRef(this, Interop.TessApi.Native.MonitorCreate());

            onProgressDelegate = OnProgress;
            onCancelCheckDelegate = OnCancelCheck;
        }

        private bool OnProgress(IntPtr ths, int left, int right, int top, int bottom)
        {
            // Avoid throwing exceptions back into native code
            if (IsDisposed)
                return true;

            // default_progress_func calls progress_callback, which is only used by test code
            // We could retrieve the original value and call it back here, but there's currently no point to doing so

            progressEvent?.Invoke(this, new MonitorProgressEventArgs(left, right, top, bottom, GetProgress()));
            return true;
        }

        private bool OnCancelCheck(IntPtr cancel_this, int words)
        {
            // Avoid throwing exceptions back into native code
            if (IsDisposed)
                return true;

            var cancelCheck = cancelCheckEvent;
            if (cancelCheck != null)
            {
                var args = new MonitorCancelEventArgs(words, GetProgress());
                cancelCheck.Invoke(this, args);
                return args.Cancel;
            }
            return false;
        }

        public int GetProgress()
        {
            VerifyNotDisposed();
            if (_handleRef.Handle == IntPtr.Zero)
                return 0;

            return Interop.TessApi.Native.MonitorGetProgress(_handleRef);
        }

        public void SetDeadline(int milliseconds)
        {
            VerifyNotDisposed();
            if (_handleRef.Handle == IntPtr.Zero)
                return;

            Interop.TessApi.Native.MonitorSetDeadlineMSecs(_handleRef, milliseconds);
        }

        protected override void Dispose(bool disposing)
        {
            if (!IsDisposed && _handleRef.Handle != IntPtr.Zero)
            {
                Interop.TessApi.Native.MonitorDelete(_handleRef);
            }
        }
    }
}
