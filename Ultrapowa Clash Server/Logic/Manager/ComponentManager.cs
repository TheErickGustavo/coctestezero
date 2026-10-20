using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Windows;
using Newtonsoft.Json;
using UCS.PacketProcessing;
using UCS.Core;
using UCS.GameFiles;

namespace UCS.Logic
{
    class ComponentManager
    {
        private List<List<Component>> m_vComponents;
        private Level m_vLevel;

        public ComponentManager(Level l) 
        {
            //m_vComponents = new List<Component>();

            m_vComponents = new List<List<Component>>();
            for (int i = 0; i <= 10 ; i++)
            {
                m_vComponents.Add(new List<Component>());
            }

            m_vLevel = l;
        }

        public void AddComponent(Component c)
        {
            m_vComponents[c.Type].Add(c);
        }

        public Component GetClosestComponent(int x, int y, ComponentFilter cf)
        {
            Component result = null;
            int componentType = cf.Type;
            List<Component> components = m_vComponents[componentType];
            Vector v = new Vector(x,y);
            double maxLengthSquared = 0;

            if (components.Count > 0)
            {
                foreach(var c in components)
                {
                    if(cf.TestComponent(c))
                    {
                        GameObject go = c.GetParent();
                        double lengthSquared = (v - go.GetPosition()).LengthSquared;
                        if(lengthSquared < maxLengthSquared || result == null)
                        {
                            maxLengthSquared = lengthSquared;
                            result = c;
                        }
                    }
                    else
                    {
                        //R-C5: diagnostico - o filtro do storage rejeitou o predio
                        GameObject go = c.GetParent();
                        Building gb = go as Building;
                        Component gc = go.GetComponent(0, false);
                        Console.WriteLine("[UCS] R-C5 filtro: predio " +
                            (gb != null && gb.GetBuildingData() != null ? gb.GetBuildingData().GetGlobalID() + " " + gb.GetBuildingData().TID : "?") +
                            " componentType=" + (gc != null ? gc.Type.ToString() : "null") +
                            " enabled=" + (gc != null && gc.IsEnabled() ? "sim" : "NAO"));
                    }
                }
            }
            return result;
        }

        public List<Component> GetComponents(int type)
        {
            return m_vComponents[type];
        }

        public int GetMaxBarrackLevel()
        {
            int result = 0;
            List<Component> components = m_vComponents[3];
            if(components.Count > 0)
            {
                foreach(UnitProductionComponent c in components)
                {
                    if(!c.IsSpellForge())
                    {
                        int level = ((Building)c.GetParent()).GetUpgradeLevel();
                        if (level > result)
                            result = level;
                    }
                }
            }
            return result;
        }

        public int GetMaxSpellForgeLevel()
        {
            int result = 0;
            List<Component> components = m_vComponents[3];
            if (components.Count > 0)
            {
                foreach (UnitProductionComponent c in components)
                {
                    if (c.IsSpellForge())
                    {
                        Building b = (Building)c.GetParent();
                        if(!b.IsConstructing() || b.IsUpgrading())
                        {
                            int level = b.GetUpgradeLevel();
                            if (level > result)
                                result = level;
                        }
                    }
                }
            }
            return result;
        }

        public int GetTotalMaxHousing(bool IsSpellForge = false)
        {
            int result = 0;
            var components = m_vComponents[0];
            if (components.Count >= 1)
                foreach (var c in components)
                {
                    if (((UnitStorageComponent)c).IsSpellForge == IsSpellForge)
                        result += ((UnitStorageComponent)c).GetMaxCapacity();
                }
            return result;
        } 

        public int GetTotalUsedHousing(bool IsSpellForge = false)
        {
            int result = 0;
            var components = m_vComponents[0];
            if(components.Count >= 1)
            foreach (var c in components)
            {
                if (((UnitStorageComponent)c).IsSpellForge == IsSpellForge)
                    result += ((UnitStorageComponent)c).GetUsedCapacity();
            }
            return result;
        }

        public void RefreshResourcesCaps()
        {
            var table = ObjectManager.DataTables.GetTable(2);
            int resourceCount = table.GetItemCount();
            int resourceStorageComponentCount = GetComponents(6).Count;
            for(int i = 0; i < resourceCount; i++)
            {
                int resourceCap = 0;
                for(int j = 0; j < resourceStorageComponentCount ; j++)
                {
                    var res = (ResourceStorageComponent)GetComponents(6)[j];
                    if(res.IsEnabled())
                        resourceCap += res.GetMax(i);
                    var resource = (ResourceData)table.GetItemAt(i);
                    if(!resource.PremiumCurrency)
                    {
                        m_vLevel.GetPlayerAvatar().SetResourceCap(resource, resourceCap);
                    }
                }
            }
        }

        public void RemoveGameObjectReferences(GameObject go)
        {
            foreach (var components in m_vComponents)
            {
                var markedForRemoval = new List<Component>();//can't remove in foreach so create a temp structure to store components to remove
                foreach(var component in components)
                {
                    if (component.GetParent() == go)
                        markedForRemoval.Add(component);
                }
                foreach (var component in markedForRemoval)
                    components.Remove(component);
            }
        }

        public void Tick()
        {
            //R-C5: este metodo estava VAZIO - nenhum component era tickado, logo a
            //producao de tropas (UnitProductionComponent.Tick -> ProductionCompleted)
            //NUNCA rodava no servidor. A tropa so aparebia no storage quando o jogador
            //usava gemas (SpeedUp), por isso o servidor ficava sempre atras do cliente
            //no numero de tropas (acampamento x multiplayer).
            for (int type = 3; type <= 3; type++)
            {
                List<Component> components = m_vComponents[type];
                for (int i = 0; i < components.Count; i++)
                {
                    try
                    {
                        components[i].Tick();
                    }
                    catch { }
                }
            }
            //R-C6: type 6 = ResourceStorageComponent (producao/coleta de elixir e ouro)
            List<Component> resStorage = m_vComponents[6];
            for (int i = 0; i < resStorage.Count; i++)
            {
                try
                {
                    resStorage[i].Tick();
                }
                catch { }
            }
        }
    }
}
