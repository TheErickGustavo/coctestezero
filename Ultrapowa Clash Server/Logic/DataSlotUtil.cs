using System;
using UCS.GameFiles;
using UCS.Core;

namespace UCS.Logic
{
    //R-T7: o "global id" que o CLIENTE 7.65 entende de um item.
    //O Data.GetGlobalID() do servidor usa um "count" parcial (bug do UCS) e devolve
    //ids DESLOCADOS: o Dragão (linha 49 do characters.csv) virava 4000006, e o
    //cliente ignorava a tropa (o castelo do cla nunca aparecia na tela de ataque).
    //O id real e 4000000 + a linha do item na tabela (Dragon = 4000049, como no
    //jogo de verdade).
    static class DataSlotUtil
    {
        public static int GetClientGlobalId(Data data)
        {
            if (data == null)
                return 0;
            try
            {
                DataTable dt = ObjectManager.DataTables.GetTable(data.GetDataType());
                if (dt != null)
                {
                    //R-T7: indice do TIPO (a ordem dos tipos no CSV) - e o que o
                    //cliente 7.65 usa (gigante = 4000003, dragao = 4000008).
                    int typeIndex = dt.GetTypeIndexOf(data);
                    if (typeIndex >= 0)
                        return GlobalID.CreateGlobalID(dt.GetTableIndex() + 1, typeIndex);
                }
            }
            catch { }
            return data.GetGlobalID();
        }
    }
}
