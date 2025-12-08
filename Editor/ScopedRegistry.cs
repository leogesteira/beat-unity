using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Text;
using System.Collections.Generic;
using System.Linq;


namespace Beat.UPM.Editor
{


    [Serializable]
    public class ScopedRegistry
    {
        public string name;
        public string url;
        public List<string> scopes;
    }
}