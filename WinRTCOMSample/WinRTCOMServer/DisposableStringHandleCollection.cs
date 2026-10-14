using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.Win32;

namespace WinRTCOMServer
{
    internal sealed partial class DisposableStringHandleCollection : ReadOnlyCollection<WindowsDeleteStringSafeHandle>, IDisposable
    {
        public DisposableStringHandleCollection(IEnumerable<string> RawStrings) : base([])
        {
            foreach (string RawString in RawStrings)
            {
                if (PInvoke.WindowsCreateString(RawString, (uint)RawString.Length, out WindowsDeleteStringSafeHandle ClassIdHandle).Succeeded)
                {
                    Items.Add(ClassIdHandle);
                }
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (Count > 0)
            {
                foreach(WindowsDeleteStringSafeHandle Handle in this.Where((Handle) => !Handle.IsInvalid))
                {
                    Handle.Dispose();
                }
            }
        }

        ~DisposableStringHandleCollection()
        {
            Dispose();
        }
    }
}
