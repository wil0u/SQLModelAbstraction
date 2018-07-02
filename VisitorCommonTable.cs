using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Collections.Generic;

namespace TransactSqlScriptDomTest
{
    internal class VisitorCommonTable : TSqlFragmentVisitor
    {
        public int id_requete_courante = -1;
        public Dictionary<int, List<TSqlFragment>> dict = new Dictionary<int, List<TSqlFragment>>();

        public override void Visit(CommonTableExpression node)
        {
            if (dict.ContainsKey(this.id_requete_courante))
            {
                this.dict[id_requete_courante].Add(node);
            }
            else
            {
                List<TSqlFragment> listCommonTable = new List<TSqlFragment>();
                listCommonTable.Add(node);
                this.dict.Add(id_requete_courante, listCommonTable);
            }

        }

    }
}