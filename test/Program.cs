using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Linq;
using System.Collections;
using System.Xml;

namespace TransactSqlScriptDomTest
{
    class Program
    {
        static string path = @"C:\Users\wilou\source\repos\SqlShareParsing\SqlShareParsing\ressources\queries.txt";
        static void Main(string[] args)
        {

            /*Lire fichier*/
            File.Delete(@"C:\Users\wilou\source\repos\SqlShareParsing\SqlShareParsing\ressources\fichier_abstraction\test.xml");
            string text = System.IO.File.ReadAllText(path);
            string[] queries = text.Split("________________________________________");
            /*Initialisation parser*/
            var parser = new TSql130Parser(false);
            /*Stocke les erreurs liés à la lecture du parser*/
            IList<ParseError> errors;
            /*Iniatilisation*/
            string text2 = System.IO.File.ReadAllText(@"C:\Users\wilou\source\repos\SqlShareParsing\SqlShareParsing\ressources\view_script.txt");
            Dictionary<String, List<String>> PhysicalTableList = new Dictionary<String, List<String>>();
            string[] views = Regex.Split(text2, "________________________________________");
            String sDir = @"C:\Users\wilou\Documents\stage_workspace\sqlshare_data_release1\data";
            foreach (String view in views)
            {
                Regex rx = new Regex(@"\(\[.*\]\)",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);
                Regex rx2 = new Regex(@"\[[^\[\]\(\)]*\]\.\[[^\[\]]*\]",
              RegexOptions.Compiled | RegexOptions.IgnoreCase);
                // Find matches.
                String matchText = "";
                String matchText2 = "";
                MatchCollection matches = rx.Matches(view);
                MatchCollection matches2 = rx2.Matches(view);
                if (matches.Count > 0)
                {
                    matchText = matches[0].Groups[0].Value;
                }
                if (matches2.Count > 0)
                {
                    matchText2 = matches2[0].Groups[0].Value;
                }
                if (!PhysicalTableList.ContainsKey(matchText2))
                {
                    using (StreamWriter sw = File.AppendText(@"C:\Users\wilou\source\repos\SqlShareParsing\SqlShareParsing\ressources\myf.txt"))
                    {

                        sw.WriteLine(matchText2 + " " + matchText.Replace("(", "").Replace(")", ""));

                    }
                    //Console.WriteLine("le match est : " + matchText.Replace("(", "").Replace(")", "") + ", pour la vue nommée : " + matchText2);
                    PhysicalTableList.Add(matchText2, new List<String>(matchText.Replace("(", "").Replace(")", "").Split(',')));
                }
            }
            try
            {
                string firstLine;
                foreach (string d in Directory.GetDirectories(sDir))
                {
                    foreach (string f in Directory.GetFiles(d))
                    {
                        using (StreamReader reader = new StreamReader(f))
                        {
                            firstLine = reader.ReadLine() ?? "";
                        }
                        string[] listIdentifier = f.Split('\\');
                        //Avant-dernière value du chemin de dossier pour récupérer le nom d'utilisateur
                        int pos1 = listIdentifier.Count() - 2;
                        //dernière value .. 
                        int pos2 = listIdentifier.Count() - 1;
                        //Console.WriteLine("[" + listIdentifier[pos1] + "].[" + listIdentifier[pos2] + "] = " + firstLine);
                        PhysicalTableList.Add("[" + listIdentifier[pos1] + "].[" + listIdentifier[pos2] + "]", new List<String>(firstLine.Split(',')));
                    }
                }
            }
            catch (System.Exception excpt)
            {
                Console.WriteLine(excpt.Message);
            }
            MyVisitor myvisitor = new MyVisitor();
            VisitorCommonTable myVisitorCommonTable = new VisitorCommonTable();
            myvisitor.PhysicalTableList = PhysicalTableList;
            int i = 0;
            foreach (String query in queries)
            {
                myVisitorCommonTable.id_requete_courante = i;
                var fragment = parser.Parse(new StringReader(query), out errors);
                fragment.Accept(myVisitorCommonTable);
                i = i + 1;
            }
            myvisitor.dictTableWith = myVisitorCommonTable.dict;

            i = 0;
            /*Pour chaque requête présent dans le fichier, on l'analyse*/
            foreach (var query in queries)
            {
                /*Sépare chaque query et leur résultat par "______________" pour le fichier de sorti*/
                myvisitor.save(query, i.ToString());
                //Console.WriteLine(Environment.NewLine+"_____________________________"+query);
                /*Enregistre la query en cours*/
                TSqlFragment fragment = parser.Parse(new StringReader(query), out errors);
                fragment.Accept(myvisitor);
                myvisitor.add();
                i++;
                Console.WriteLine(i);
            }
            /*Remise à zéro du string pour les clauses présentes dans le FROM, pour éviter les doublons avec le WhereClause*/
            Console.WriteLine("FIN");
            Console.ReadLine();
            myvisitor.Imprime();

        }
    }

    internal class MyVisitor : TSqlFragmentVisitor
    {
        Boolean filtreWith = false;
        private string id = "";
        private string requete = "";
        private string selection = "";
        private string aggregate = "";
        private string projection = "";
        private string fromClause = "";
        private string filtreHaving = "";
        private XmlDocument doc = null;
        int id_requete_courante = -1;
        int id_requete_precedante = -1;
        public String user_courant = "";
        public String user_precedant = "";
        public Dictionary<int, List<TSqlFragment>> dictTableWith = new Dictionary<int, List<TSqlFragment>>();
        public Dictionary<string, List<string>> PhysicalTableList = new Dictionary<string, List<string>>();
        public List<String> previousProjectionList = new List<String>();
        public Dictionary<int, List<String>> ProjectionListPerQuery = new Dictionary<int, List<String>>();
        public List<String> projectionList;
        public int id_explo = 0;
        private string GetNodeTokenText(TSqlFragment fragment)
        {
            StringBuilder tokenText = new StringBuilder();
            for (int counter = fragment.FirstTokenIndex; counter <= fragment.LastTokenIndex; counter++)
            {
                tokenText.Append(fragment.ScriptTokenStream[counter].Text);
            }
            return tokenText.ToString();
        }

        public override void Visit(QuerySpecification node)
        {
            WhereClause lol = node.WhereClause;
            HavingClause xD = node.HavingClause;
            FromClause trucs = node.FromClause;
            /* if (trucs != null)
                 if (GetNodeTokenText(trucs).ToLower().Contains("where"))
                 {
                     string tmp = GetNodeTokenText(trucs).Substring(GetNodeTokenText(trucs).IndexOf("(") + 1,GetNodeTokenText(trucs).Length - GetNodeTokenText(trucs).IndexOf("(")-1);
                     tmp = tmp.Substring(0, tmp.LastIndexOf(')'));
                     test += tmp + "|";
                     if (Regex.Matches(tmp.ToLower(), "select").Count > 1)
                     {
                         String[] queries = tmp.ToLower().Split("select");
                         foreach (var query in queries)
                             if (query.Contains("where"))
                                 test += "select "+ query + "|";
                     }
                 }*/
            if (lol != null)
            {
                if (!GetNodeTokenText(lol).ToLower().Contains("select"))
                    seperate(GetNodeTokenText(lol).ToLower().Replace("where", ""));
                else
                    seperate(GetNodeTokenText(lol).ToLower().Substring(6, GetNodeTokenText(lol).Length - 6));
            }
            if (xD != null)
            {
                string havingClause = GetNodeTokenText(xD).ToLower().Substring(6, GetNodeTokenText(xD).Length - 6);
                havingClause = havingClause.Replace("\r\n", "");
                Regex rgx = new Regex(@"\s+");
                havingClause = rgx.Replace(havingClause, " ");
                selection += "| " + havingClause;
                filtreHaving = havingClause;
            }

            /* MY BIG PART OF CODE ! mouahaha*/
            id_requete_courante = Int32.Parse(this.id);
            node.ToString();

            bool starExpression = false;

            if (this.id_requete_courante != this.id_requete_precedante)
            {
                projectionList = new List<String>();
                Regex rxUser = new Regex(@"\[[^\[\]\(\)]*\]\.\[[^\[\]\(\)]*\]",
RegexOptions.Compiled | RegexOptions.IgnoreCase);
                // Find matches.
                //Console.WriteLine(this.PhysicalTableList["[1002].[Tokyo_0_merged.csv]"]);
                MatchCollection matchesUser = rxUser.Matches(this.requete);
                if (matchesUser.Count > 0)
                {
                    String matchTexte = matchesUser[0].Groups[0].Value;
                    this.user_courant = matchTexte.Split('.')[0].Replace("[", "").Replace("]", "");

                }
                if (node.SelectElements != null)
                {
                    foreach (TSqlFragment selectElement in node.SelectElements)
                    {
                        if (selectElement is SelectStarExpression)
                        {
                            starExpression = true;
                            if (node.SelectElements.Count > 1)
                            {
                                //Lever une exception qui dit que c'est non géré*

                                break;
                            }
                            else
                            {
                                if (node.FromClause != null)
                                {
                                    foreach (TSqlFragment fromElement in node.FromClause.TableReferences)
                                    {
                                        if (fromElement is NamedTableReference)
                                        {
                                            String namedTableReferenceText = GetNodeTokenText(fromElement);
                                            if (this.dictTableWith.ContainsKey(this.id_requete_courante))
                                            {
                                                foreach (CommonTableExpression commonTableExpression in this.dictTableWith[this.id_requete_courante])
                                                {
                                                    if (commonTableExpression.ExpressionName.Value == namedTableReferenceText)
                                                    {
                                                        if (commonTableExpression.QueryExpression is QuerySpecification)
                                                        {
                                                            QuerySpecification queryUsedForTableDefinition = (QuerySpecification)commonTableExpression.QueryExpression;
                                                            foreach (SelectElement selectElementInQueryUsedForTableDefinition in queryUsedForTableDefinition.SelectElements)
                                                            {
                                                                projectionList.Add(GetNodeTokenText(selectElementInQueryUsedForTableDefinition));
                                                            }
                                                        }

                                                    }
                                                }
                                            }
                                            else
                                            {
                                                Regex rx = new Regex(@"\[.*\]\.\[.*\]",
      RegexOptions.Compiled | RegexOptions.IgnoreCase);
                                                // Find matches.
                                                //Console.WriteLine(this.PhysicalTableList["[1002].[Tokyo_0_merged.csv]"]);
                                                String matchText = "";
                                                MatchCollection matches = rx.Matches(namedTableReferenceText);
                                                if (matches.Count > 0)
                                                {
                                                    matchText = matches[0].Groups[0].Value;
                                                }
                                                if (this.PhysicalTableList.ContainsKey(matchText.ToLower()))
                                                {
                                                    foreach (String attribut in this.PhysicalTableList[matchText.ToLower()])
                                                    {
                                                        projectionList.Add(attribut);
                                                    }
                                                }

                                            }
                                        }
                                    }
                                }
                            }
                        }

                    }
                    if (starExpression == false)
                    {
                        foreach (SelectElement selectElement in node.SelectElements)
                        {
                            if (GetNodeTokenText(selectElement).Contains(','))
                            {
                                Console.WriteLine(GetNodeTokenText(selectElement));
                            }
                            projectionList.Add(GetNodeTokenText(selectElement));
                        }
                    }
                }

                ProjectionListPerQuery.Add(this.id_requete_courante, projectionList);
                int nombreProjectionsCommunes = 0;
                int nombreProjections = projectionList.Count;
                if (user_precedant.Equals("") || !user_courant.Equals(user_precedant))
                {
                    nombreProjectionsCommunes = 0;
                    if (!user_courant.Equals(user_precedant))
                    {
                        this.id_explo = this.id_explo + 1;
                    }
                }
                else
                {
                    if (user_precedant.Equals(user_courant))
                    {
                        nombreProjectionsCommunes = previousProjectionList.Intersect(projectionList, StringComparer.InvariantCultureIgnoreCase).ToList().Count;
                        //Faire l'intersection
                    }
                }
                using (StreamWriter sw = File.AppendText(@"C:\Users\wilou\source\repos\SqlShareParsing\SqlShareParsing\ressources\explo.txt"))
                {

                    sw.WriteLine(this.id_requete_courante + ";" + user_courant + ";" + this.id_explo);
                }
                previousProjectionList = projectionList;
                user_precedant = user_courant;

                using (StreamWriter sw = File.AppendText(@"C:\Users\wilou\source\repos\SqlShareParsing\SqlShareParsing\ressources\myfriend.txt"))
                {

                    sw.Write("\n-------\nProjection pour la requête : " + this.requete);
                    //sw.Write("Projections pour la requête : " + text_requete_courante);
                    Console.WriteLine(" Sont : ");
                    sw.Write("\n Sont :");

                    foreach (String projection in projectionList)
                    {
                        Console.WriteLine(projection);
                        sw.Write("\n" + projection);

                    }
                    sw.Write("\n--------\n");
                }
            }

            this.id_requete_precedante = this.id_requete_courante;

        }
        public override void Visit(DeleteSpecification node)
        {
            WhereClause lol = node.WhereClause;
            if (lol != null)
                if (!GetNodeTokenText(lol).ToLower().Contains("select"))
                    seperate(GetNodeTokenText(lol).ToLower().Replace("where", ""));
                else
                    seperate(GetNodeTokenText(lol).ToLower().Substring(6, GetNodeTokenText(lol).Length - 6));
        }
        public override void Visit(WithCtesAndXmlNamespaces node)
        {
            /*Savoir s'il existe un with*/
            filtreWith = true;
            string fromWith = GetNodeTokenText(node).ToLower();
            /*On récupère la clause qui nous intéresse*/
            Regex regex = new Regex(@"(from[\s+]*(\[[A-z.0-9]*\].\[[A-z.0-9]*.*\][ ]*[A-z0-9]*)([\s+]*,[ ]*\[[0-9A-z.]*\].\[[A-z0-9.]*\][ ]*[A-z0-9]*)?|join[\s+]*\[[A-z .0-9]*\][ ]*[A-z0-9]*|join[\s+]+[\[\]A-z0-9.]*|from[/s+]*[A-z0-9]*\.[A-z0-9]*|from[\s+]*[A-z ]*[ ]*[A-z0-9]|from[\s+]\[[A-z.0-9 ]*\][ ]*[A-z0-9]*)");
            Match match = regex.Match(fromWith);
            fromClause = "";
            /*Tant qu'il y a des from qui nous intéresse*/


            /*on extrait la clause*/
            string tmp = fromWith.Substring(match.Index, match.Length);
            /*on met à jour le node*/
            fromWith = fromWith.Substring(0, match.Index) + " " + fromWith.Substring(match.Index + match.Length, fromWith.Length - match.Index - match.Length);
            /*on supprime les join et from*/
            tmp = (new Regex(@"from |join ")).Replace(tmp, "");
            match = regex.Match(fromWith);
            /*s'il existe plusieurs tables séparées par ,*/
            foreach (var seperate in tmp.Split(","))
            {
                if (!tmp.Equals("") && !fromClause.Contains(seperate) && !selection.Contains(seperate))
                    fromClause += " | " + seperate;
            }

        }


        public override void Visit(FunctionCall node)
        {
            string study = GetNodeTokenText(node);

            Regex isAggregate = new Regex(@"(sum|avg|checksum_agg|count|count_big|grouping|grouping_id|max|min|stdev|stdevp|string_agg|var|varp)[\s+]*\([*_.\(\)""A-z0-9+=<>/\- \[\]]*\)");
            Match match = isAggregate.Match(study.ToLower());
            if (match.Success)
            {
                isAggregate = new Regex(@"[A-z_\(,\s]+(sum|avg|checksum_agg|count|count_big|grouping|grouping_id|max|min|stdev|stdevp|string_agg|var|varp)\([*_.""\(\)\[\]/A-z0-9+=<>\'-]*\)");
                if (!filtreHaving.Contains(study.ToLower()) && !isAggregate.Match(GetNodeTokenText(node).ToLower()).Success)
                    aggregate += " | " + GetNodeTokenText(node);
            }

        }

        public override void Visit(QualifiedJoin node)
        {
            string study = GetNodeTokenText(node).ToLower();
            Regex regex = new Regex(@"[(/r)\s+]+on[^A-z].*((\s+)*(substring\([A-z.,'\s+\(\)0-9]*\))|(\s+)*[A-z.0-9]*[\s+]*=[\s+]*[A-z_.0-9]*|(\s+)*[A-z.0-9]*[\s+]=[\s+]*\([A-z\s+0-9.=!\(\]]*\)|(\s+)*[A-z.(\s+)]*=[\s+]*\([A-z\s+0-9.\[\]=!><\(\),']*\)|((\s+)*(and|or).*)*)?");
            Match match = regex.Match(study);
            while (match.Success)
            {
                string tmp = study.Substring(match.Index, match.Length);
                if (tmp.Contains("or") || tmp.Contains("and"))
                    seperate(tmp);
                else
                    selection += "|" + tmp.Substring(tmp.IndexOf("on ") + 2, tmp.Length - tmp.IndexOf("on ") - 2);
                study = study.Substring(0, match.Index) + " " + study.Substring(match.Index + match.Length, study.Length - (match.Index + match.Length));
                match = regex.Match(study);
            }
            study = study.Substring(study.IndexOf("on ") + 2, study.Length - study.IndexOf("on ") - 2);
        }

        public void seperate(string pat)
        {
            String[] sep = new String[0];
            /*Pré-Traitement de la whereClause trouvé, supprime tous les sauts de ligne
             ajoute un espace avant et après une parenthèse
             remplace les espaces blancs par un seul espace
             efface les commentaires dans les where ex : 
             where --or
             x=5 +> where x=5
             */
            Regex rgx = new Regex(@"--[A-z0-9\(\)\[\],._!=<>'\s+][^\r\n]*");
            if (rgx.Match(pat).Success)
            {
                pat = rgx.Replace(pat, "");
            }

            pat = pat.Replace("\r\n", "");
            pat = pat.Replace(")", " ) ");
            pat = pat.Replace("(", " ( ");
            rgx = new Regex(@"\s+");
            pat = rgx.Replace(pat, " ");
            rgx = new Regex(@"between[\s+][A-z0-9.]*[\s+](and)[\s+][A-z0-9.]*");
            Match match = rgx.Match(pat);
            if (rgx.Match(pat).Success)
            {
                pat = rgx.Replace(pat, match.Value.Replace(" ", ""));
            }
            /*Fin Pré-Traitement*/
            /*On recherche les cas où les or et and sont dans les parenthèses si on en trouve on l'extrait de la chaine*/
            rgx = new Regex(@"[(][a-z0-9\s+=<>\[\]!'_.]*( or | and )[\(a-z0-9\s+=<>\[\]!'_.&\|]*[)]");
            match = rgx.Match(pat);
            if (match.Success)
            {
                string a = pat.Substring(match.Index, match.Length);
                if (a.Contains("between"))
                {
                    a = a.Replace("between", " between ");
                    Regex number = new Regex(@"[0-9.]+and[0-9.]+");
                    Match number2 = number.Match(a);
                    if (number2.Success)
                    {
                        string tmp = number2.Value;
                        tmp = tmp.Replace("and", " and ");
                        a = number.Replace(a, tmp);
                    }
                }
                selection += " | " + a;
                pat = pat.Substring(0, match.Index) + " " + pat.Substring(match.Index + match.Length, pat.Length - (match.Index + match.Length));
            }
            /*On split sur or ou and*/
            sep = Regex.Split(pat, @" or | and ");
            string between = "";
            foreach (var binary in sep)
            {
                if (binary.Contains("between"))
                {
                    between = binary.Replace("between", " between ");
                    Regex number = new Regex(@"[0-9.]+and[0-9.]+");
                    Match number2 = number.Match(between);
                    if (number2.Success)
                    {
                        string tmp = number2.Value;
                        tmp = tmp.Replace("and", " and ");
                        between = number.Replace(binary, tmp);
                    }
                }
                if (between.Equals(""))
                {
                    rgx = new Regex(@"\s+");
                    string tmp = rgx.Replace(binary, " ");
                    if (!tmp.Equals(" ") && !tmp.Equals(" ) "))
                        selection += " | " + binary;
                }
                else
                {
                    selection += " | " + between;
                    between = "";
                }
            }

        }

        /*Sauve un string qu'on souhaite voir dans le fichier de sortie*/
        public void save(string tmp, String i)
        {
            id = i;
            requete += tmp;

        }
        public override void Visit(FromClause node)
        {

            string from = GetNodeTokenText(node).ToLower();
            /*supprime les possibles commentaires*/
            Regex rgx = new Regex(@"--[A-z0-9\(\)\[\],._!=<>'\s+][^\r\n]*");
            if (rgx.Match(from).Success)
            {
                from = rgx.Replace(from, "");
            }
            /*Regex pour récupérer les formes clauses*/
            rgx = new Regex(@"(from[\s+]*(\[[A-z.0-9]*\].\[[A-z.0-9]*.*\][ ]*[A-z0-9]*)([\s+]*,[ ]*\[[0-9A-z.]*\].\[[A-z0-9.]*\][ ]*[A-z0-9]*)?|join[\s+]*\[[A-z .0-9]*\][ ]*[A-z0-9]*|join[\s+]+[\[\]A-z0-9.]*|from[/s+]*[A-z0-9]*\.[A-z0-9]*|from[\s+]*[A-z ]*[ ]*[A-z0-9]|from[\s+]\[[A-z.0-9 ]*\][ ]*[A-z0-9]*)");
            Match match = rgx.Match(from);
            /*tant qu'il existe une forme clause à récupérer*/
            while (match.Success)
            {
                /*On l'extrait de from et on la stock dans tmp*/
                string tmp = from.Substring(match.Index, match.Length);
                /*on met à jour from sans tmp*/
                from = from.Substring(0, match.Index) + " " + from.Substring(match.Index + match.Length, from.Length - match.Index - match.Length);
                /*On regarde s'il existe une autre clause à récupérer*/
                match = rgx.Match(from);
                /*on supprime le from ou join*/
                tmp = (new Regex(@"from |join ")).Replace(tmp, "");
                /*s'il y a plusieurs tables ex [896].[lol], [896].[xD]*/
                foreach (var seperate in tmp.Split(","))
                {
                    /*si la table n'existe pas déjà et s'il elle n'est pas inclus dans une sélection, ni dans un filtre with*/
                    if (!tmp.Equals("") && !fromClause.Contains(seperate) && !selection.Contains(seperate) && !filtreWith)
                        fromClause += " | " + seperate;
                }
            }

        }

        /*Créer le fichier de sortie, et écrit le contenu de test*/
        public void ecrireFichier()
        {
            XmlElement nouveau = null;
            XmlElement noeud = null;
            XmlElement sousn = null;
            try
            {
                nouveau = doc.CreateElement("requête");

                noeud = doc.CreateElement("id");
                noeud.InnerText = id;
                nouveau.AppendChild(noeud);

                noeud = doc.CreateElement("request");
                noeud.InnerText = requete;
                nouveau.AppendChild(noeud);

                noeud = doc.CreateElement("projections");
                foreach (var element in this.projectionList)
                {
                    sousn = doc.CreateElement("projection");
                    sousn.InnerText = element;
                    noeud.AppendChild(sousn);
                }
                nouveau.AppendChild(noeud);

                noeud = doc.CreateElement("selections");
                if (!selection.Equals(""))
                    foreach (var select in selection.Split("|"))
                    {

                        if (!select.Equals("") || !select.Equals(" "))
                        {
                            sousn = doc.CreateElement("selection");
                            sousn.InnerText = select;
                            noeud.AppendChild(sousn);

                        }
                    }
                nouveau.AppendChild(noeud);

                noeud = doc.CreateElement("aggregates");
                foreach (var agg in aggregate.Split("|"))
                {
                    if (!agg.Equals(""))
                    {

                        sousn = doc.CreateElement("aggregate");
                        sousn.InnerText = agg;
                        noeud.AppendChild(sousn);
                    }
                }
                nouveau.AppendChild(noeud);

                noeud = doc.CreateElement("fromClause");
                foreach (var from in fromClause.Split("|"))
                {
                    if (!from.Equals(""))
                    {
                        sousn = doc.CreateElement("from");
                        sousn.InnerText = from;
                        noeud.AppendChild(sousn);
                    }
                }
                nouveau.AppendChild(noeud);
                XmlElement el = (XmlElement)doc.SelectSingleNode("requêtes");
                el.AppendChild(nouveau);

            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }

        }

        public void add()
        {
            if (doc == null)
            {
                doc = new XmlDocument();
                XmlDeclaration dec = doc.CreateXmlDeclaration("1.0", "UTF-8", "yes");
                doc.AppendChild(dec);
                XmlElement root = doc.CreateElement("requêtes");
                doc.AppendChild(root);
            }
            selection = selection.Substring(selection.IndexOf("|") + 1, selection.Length - selection.IndexOf("|") - 1);
            aggregate = aggregate.Substring(aggregate.IndexOf("|") + 1, aggregate.Length - aggregate.IndexOf("|") - 1);
            fromClause = fromClause.Substring(fromClause.IndexOf("|") + 1, fromClause.Length - fromClause.IndexOf("|") - 1);
            this.ecrireFichier();
            id = "";
            requete = "";
            selection = "";
            aggregate = "";
            fromClause = "";
            filtreHaving = "";
        }
        public void Imprime()
        {
            doc.Save(@"C:\Users\wilou\source\repos\SqlShareParsing\SqlShareParsing\ressources\fichier_abstraction\test.xml");
        }
    }
}
