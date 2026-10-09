using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Timers;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.System.WinRT;
using WinRT;
using Timer = System.Timers.Timer;

namespace WinRTCOMServer
{
    internal class Program
    {
        private static readonly Timer IdleTimer = new Timer(TimeSpan.FromSeconds(30));
        private static readonly ManualResetEvent ShutdownEvent = new ManualResetEvent(false);
        private static readonly string[] ExportedClassNames = [
            typeof(COMService).FullName,
        ];

        static Program()
        {
            IdleTimer.AutoReset = false;
            IdleTimer.Elapsed += IdleTimer_Elapsed;
        }

        static void Main(string[] args)
        {
            PInvoke.RoInitialize(RO_INIT_TYPE.RO_INIT_MULTITHREADED).ThrowOnFailure();

            try
            {
                SetCOMRuntimeOptions();

                if (ExportedClassNames.Length > 0)
                {
                    RO_REGISTRATION_COOKIE Cookie;

                    using (DisposableStringHandleCollection ClassNameSafeHandles = new DisposableStringHandleCollection(ExportedClassNames))
                    {
                        unsafe
                        {
                            delegate* unmanaged[Stdcall]<HSTRING, IActivationFactory**, HRESULT>[] Callbacks = new delegate* unmanaged[Stdcall]<HSTRING, IActivationFactory**, HRESULT>[ClassNameSafeHandles.Count];

                            for (int Index = 0; Index < Callbacks.Length; Index++)
                            {
                                Callbacks[Index] = &GetActivationFactory;
                            }

                            fixed (delegate* unmanaged[Stdcall]<HSTRING, IActivationFactory**, HRESULT>* CallbacksPtr = Callbacks)
                            {
                                PInvoke.RoRegisterActivationFactories([.. ClassNameSafeHandles.Select((Handle) => (HSTRING)Handle.DangerousGetHandle())], CallbacksPtr, out Cookie).ThrowOnFailure();
                            }
                        }
                    }

                    try
                    {
                        ShutdownEvent.WaitOne();
                    }
                    finally
                    {
                        PInvoke.RoRevokeActivationFactories(Cookie);
                    }
                }

            }
            finally
            {
                PInvoke.RoUninitialize();
            }
        }

        internal static void AddServerReference()
        {
            PInvoke.CoAddRefServerProcess();

            if (IdleTimer.Enabled)
            {
                IdleTimer.Stop();
            }
        }

        internal static void RemoveServerReference()
        {
            if (PInvoke.CoReleaseServerProcess() == 0)
            {
                IdleTimer.Start();
            }
        }

        private static void IdleTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ShutdownEvent.Set();
        }

        private static unsafe void SetCOMRuntimeOptions()
        {
            if (PInvoke.CoCreateInstance(PInvoke.CLSID_GlobalOptions, null, CLSCTX.CLSCTX_INPROC_SERVER, out IGlobalOptions* Options).Failed)
            {
#if DEBUG
                if (Debugger.IsAttached)
                {
                    Debug.WriteLine("Failed to create COM global options instance, global options might not be applied.");
                    Debugger.Break();
                }
#endif
                return;
            }

            // COMGLB_EXCEPTION_DONOT_HANDLE_ANY is suggested for new application and OOP server to avoid the overhead of handling exceptions in the COM runtime.
            if (Options->Set(GLOBALOPT_PROPERTIES.COMGLB_EXCEPTION_HANDLING, new UIntPtr((uint)GLOBALOPT_EH_VALUES.COMGLB_EXCEPTION_DONOT_HANDLE_ANY)).Failed)
            {
#if DEBUG
                if (Debugger.IsAttached)
                {
                    Debug.WriteLine("Failed to set COM global options, exception handling behavior might be suboptimal.");
                    Debugger.Break();
                }
#endif
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static unsafe HRESULT GetActivationFactory(HSTRING ActivatableClassId, IActivationFactory** Factory)
        {
            *Factory = null;

            if (ActivatableClassId.IsNull)
            {
                return HRESULT.E_INVALIDARG;
            }

            try
            {
                *Factory = (IActivationFactory*)Module.GetActivationFactory(MarshalString.FromAbi((IntPtr)ActivatableClassId));
            }
            catch (Exception e)
            {
                ExceptionHelpers.SetErrorInfo(e);
                return (HRESULT)ExceptionHelpers.GetHRForException(e);
            }

            if (*Factory != null)
            {
                return HRESULT.S_OK;
            }

            return HRESULT.CLASS_E_CLASSNOTAVAILABLE;
        }
    }
}
