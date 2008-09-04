using System;
using System.Collections.Generic;
using System.Text;
using System.Management.Automation;
using System.ComponentModel;

namespace BSDZ
{
  [RunInstaller(true)]
  public class BSDZ : PSSnapIn
  {
    public override string Name
    {
      get { return "BSDZ"; }
    }
    public override string Vendor
    {
      get { return "Blair Sutton"; }
    }
    public override string VendorResource
    {
      get { return "ProcessPipe,"; }
    }
    public override string Description
    {
      get { return "Registers the CmdLets and Providers in this assembly"; }
    }
    public override string DescriptionResource
    {
      get { return "ProcessPipe,Registers the CmdLets and Providers in this assembly"; }
    }
  }
}
