using System;
using System.Linq;

namespace Calloatti
{
  public static class Util
  {
    public static bool IsModEnabled(string assemblyName)
    {
      if (string.IsNullOrEmpty(assemblyName)) return false;

      return AppDomain.CurrentDomain.GetAssemblies()
        .Any(a => a.GetName().Name.Equals(assemblyName, StringComparison.OrdinalIgnoreCase));
    }
  }
}