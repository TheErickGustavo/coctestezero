using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using UCS.Logic;

namespace UCS.GameFiles
{
    class DataTables
    {
        private List<DataTable> m_vDataTables;

        public DataTables()
        {
            m_vDataTables = new List<DataTable>();
            for (int i = 0; i < 28; i++)
                m_vDataTables.Add(new DataTable());
        }

        public void InitDataTable(CSVTable t, int index)
        {
            if (index == 13)
                m_vDataTables[index] = new Globals(t, index);
            else
                m_vDataTables[index] = new DataTable(t, index);
        }   

        public CharacterData GetCharacterByName(string name)
        {
            DataTable dt = m_vDataTables[3];
            return (CharacterData)dt.GetDataByName(name);
        }

        public Data GetDataById(int id)
        {
            int classId = GlobalID.GetClassID(id) - 1;
            DataTable dt = m_vDataTables[classId];
            //R-T7: o cliente 7.65 referencia as tropas pelo indice do TIPO (gigante =
            //4000003, dragao = 4000008), nao pela linha do CSV (gigante = 4000020).
            //O servidor guardava a tropa errada (e o "dragao" virava archer nivel 2).
            int typeIndex = GlobalID.GetInstanceID(id);
            if (typeIndex >= 0)
            {
                Data byType = dt.GetItemByTypeIndex(typeIndex);
                if (byType != null)
                    return byType;
            }
            return dt.GetItemById(id);
        }

        public Globals GetGlobals()
        {
            return (Globals)m_vDataTables[13];
        }

        public HeroData GetHeroByName(string name)
        {
            DataTable dt = m_vDataTables[27];
            return (HeroData)dt.GetDataByName(name);
        }

        public ResourceData GetResourceByName(string name)
        {
            DataTable dt = m_vDataTables[2];
            return (ResourceData)dt.GetDataByName(name);
        }

        public DataTable GetTable(int i)
        {
            return m_vDataTables[i];
        }

    }

}
