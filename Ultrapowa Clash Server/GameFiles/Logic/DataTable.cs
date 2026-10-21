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
    class DataTable
    {
        protected List<Data> m_vData;
        protected int m_vIndex;
        //R-T7: mapa LINHA -> indice do TIPO. O CSV de characters tem 7 linhas por
        //tipo (uma por nivel, com o TID so na primeira) e o CLIENTE 7.65 referencia as
        //tropas pelo indice do TIPO (gigante = 4000003, dragao = 4000008), enquanto o
        //servidor indexava por LINHA (gigante = 4000020). Sem esse mapa o servidor
        //trocava a tropa (o "dragao" virava archer nivel 2 etc.).
        protected Dictionary<int, int> m_vTypeIndexByRow;
        protected Dictionary<int, Data> m_vDataByTypeIndex;
        protected Dictionary<string, int> m_vTypeIndexByName;

        public DataTable()
        {
            m_vIndex = 0;
            m_vData = new List<Data>();
        }

        public DataTable(CSVTable table, int index)
        {
            m_vIndex = index;
            m_vData = new List<Data>();

            for(int i=0;i<table.GetRowCount();i++)
            {
                var row = table.GetRowAt(i);
                var data = CreateItem(row);
                m_vData.Add(data);
            }
            BuildTypeIndex();
        }

        //R-T7: monta o mapa linha -> indice do tipo (um tipo = um nome distinto no
        //CSV; no characters.csv sao 7 linhas por tipo, uma por nivel).
        protected void BuildTypeIndex()
        {
            m_vTypeIndexByRow = new Dictionary<int, int>();
            m_vDataByTypeIndex = new Dictionary<int, Data>();
            m_vTypeIndexByName = new Dictionary<string, int>();
            int typeIndex = -1;
            string lastName = null;
            for (int i = 0; i < m_vData.Count; i++)
            {
                string name = m_vData[i].GetName();
                if (name != lastName)
                {
                    typeIndex++;
                    lastName = name;
                    m_vDataByTypeIndex[typeIndex] = m_vData[i];
                    m_vTypeIndexByName[name] = typeIndex;
                }
                m_vTypeIndexByRow[i] = typeIndex;
            }
        }

        //R-T7: o item que o CLIENTE referencia pelo indice do tipo.
        public Data GetItemByTypeIndex(int typeIndex)
        {
            if (m_vDataByTypeIndex == null)
                BuildTypeIndex();
            Data result = null;
            if (m_vDataByTypeIndex.TryGetValue(typeIndex, out result))
                return result;
            return null;
        }

        public int GetTypeIndexOf(Data data)
        {
            if (data == null)
                return -1;
            int row = GetItemIndex(data);
            if (row >= 0 && m_vTypeIndexByRow != null && m_vTypeIndexByRow.ContainsKey(row))
                return m_vTypeIndexByRow[row];
            if (m_vTypeIndexByName != null && m_vTypeIndexByName.ContainsKey(data.GetName()))
                return m_vTypeIndexByName[data.GetName()];
            return -1;
        }

        public Data CreateItem(CSVRow row)
        {
            Data d = new Data(row, this);
            switch (m_vIndex)
            {
                case 0:
                    d = new BuildingData(row, this);
                    break;
                case 2:
                    d = new ResourceData(row, this);
                    break;
                case 3:
                    d = new CharacterData(row, this);
                    break;
                case 7:
                    d = new ObstacleData(row, this);
                    break;
                case 10:
                    d = new ExperienceLevelData(row, this);
                    break;
                case 11:
                    d = new TrapData(row, this);
                    break;
                case 12:
                    d = new LeagueData(row, this);
                    break;
                case 13:
                    d = new GlobalData(row, this);
                    break;
                case 14:
                    d = new TownhallLevelData(row, this);
                    break;
                case 16:
                    d = new NpcData(row, this);
                    break;
                case 17:
                    d = new DecoData(row, this);
                    break;
                case 19:
                    d = new ShieldData(row, this);
                    break;
                case 22:
                    d = new AchievementData(row, this);
                    break;
                case 23:
                    d = new Data(row, this);
                    break;
                case 24:
                    d = new Data(row, this);
                    break;
                case 25:
                    d = new SpellData(row, this);
                    break;
                case 27:
                    d = new HeroData(row, this);
                    break;
                /*case 28:
                    d = new WarData(dic);
                    break;*/
                default:
                    break;
            }
            return d;
        }

        public int GetTableIndex()
        {
            return m_vIndex;
        }

        public Data GetItemAt(int index)
        {
            return m_vData[index];
        }

        public Data GetItemById(int id)
        {
            int instanceId = GlobalID.GetInstanceID(id);
            return m_vData[instanceId];
        }

        public int GetItemCount()
        {
            return m_vData.Count;
        }

        //R-T7: indice real do item (a linha do CSV). O GetGlobalID() usa um "count"
        //parcial e devolve ids DESLOCADOS (ex.: o Dragon vira 4000006 em vez de
        //4000049) - o cliente 7.65 nao reconhece a tropa com o id errado.
        public int GetItemIndex(Data data)
        {
            if (data == null)
                return -1;
            for (int i = 0; i < m_vData.Count; i++)
            {
                if (ReferenceEquals(m_vData[i], data) || m_vData[i] == data)
                    return i;
            }
            return -1;
        }

        public Data GetDataByName(string name)
        {
            return m_vData.Find(d => d.GetName() == name);
        }

    }

}
