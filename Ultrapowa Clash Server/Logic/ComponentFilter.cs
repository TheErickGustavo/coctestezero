using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Configuration;
using UCS.PacketProcessing;
using UCS.Core;
using UCS.GameFiles;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UCS.Logic
{
    class ComponentFilter : GameObjectFilter
    {
        public int Type;//a1 + 20

        public ComponentFilter(int type) : base()
        {
            Type = type;
        }

        public override bool IsComponentFilter()
        {
            return true;
        }

        public new bool TestGameObject(GameObject go)
        {
            bool result = false;
            Component c = go.GetComponent(Type, true);
            if(c != null)
            {
                result = base.TestGameObject(go);
            }
            //R-C5: diagnostico
            if (!result)
            {
                Building gb = go as Building;
                Console.WriteLine("[UCS] R-C5 filtro2: predio " +
                    (gb != null && gb.GetBuildingData() != null ? gb.GetBuildingData().GetGlobalID() + " " + gb.GetBuildingData().TID : "?") +
                    " typeDoFiltro=" + Type + " componentDoPredio=" + (c != null ? c.GetType().Name : "NULL") +
                    " ignorados=" + (m_vIgnoredObjects != null ? m_vIgnoredObjects.Count.ToString() : "null") +
                    " goGlobalId=" + go.GlobalId);
            }
            return result;
        }

        public bool TestComponent(Component c)
        {
            GameObject go = c.GetParent();
            return TestGameObject(go);
        }
    }
}
