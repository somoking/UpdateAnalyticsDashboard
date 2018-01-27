using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UpdateEncryptionDashboard;

namespace UpdateEncryptionDashboard
{
    class Program
    {
        static void Main(string[] args)
        {
            List<statusSQL> Records = new List<statusSQL>();
            
            string TableName = (@"Placeholder");
            var directory = new DirectoryInfo(@"Placeholder");
            
            var myFile = (from f in directory.GetFiles()
                          orderby f.LastWriteTime descending
                          select f).First();
            Console.WriteLine(myFile.Extension);
            
           using (SqlConnection Connection = new SqlConnection())
            {
                string commandText = "DELETE FROM ";
                SqlCommand command = new SqlCommand(commandText, Connection);

                try
                {
                    Connection.Open();
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            if (myFile.Extension == ".xlsx")
            {
                Microsoft.Office.Interop.Excel.Application xlApp = new Microsoft.Office.Interop.Excel.Application();
                Microsoft.Office.Interop.Excel.Workbook xlWorkbook = xlApp.Workbooks.Open(myFile.FullName);
                Microsoft.Office.Interop.Excel._Worksheet xlWorksheet = xlWorkbook.Sheets[1];
                Microsoft.Office.Interop.Excel.Range xlRange = xlWorksheet.UsedRange;

                //var x = xlWorksheet.Columns.Value;
                int rowCount = xlRange.Rows.Count;
                for (int i = 1; i <= rowCount; i++)
                {
                    if (i == 1) { continue; }
                    statusSQL Record = new statusSQL();

                    Record.NodeName = xlRange.Cells[i, 1].Value2.ToString();
                    Record.Instance = xlRange.Cells[i, 2].Value2.ToString();
                    Record.DBname = xlRange.Cells[i, 3].Value2.ToString();
                    Record.LogicalFileName = xlRange.Cells[i, 4].Value2.ToString();
                    Record.PathFromSQL = xlRange.Cells[i, 5].Value2.ToString();
                    Record.FileType = xlRange.Cells[i, 6].Value2.ToString();
                    Record.NodeNameVormetric = xlRange.Cells[i, 7].Value2.ToString();
                    Record.GuardPointPath = xlRange.Cells[i, 8].Value2.ToString();
                    Record.EncryptionStatus = xlRange.Cells[i, 9].Value2.ToString();
                    Record.Application = xlRange.Cells[i, 10].Value2.ToString();
                    Record.State = xlRange.Cells[i, 11].Value2.ToString();
                    Record.Environment = xlRange.Cells[i, 12].Value2.ToString();
                    Record.LastUpdate = xlRange.Cells[i, 13].Value.ToString("dd/MM/yyyy");

                    Records.Add(Record);
                    Console.WriteLine(i);
                }
                //cleanup
                GC.Collect();
                GC.WaitForPendingFinalizers();


                //release com objects to fully kill excel process from running in the background
                Marshal.ReleaseComObject(xlRange);
                Marshal.ReleaseComObject(xlWorksheet);

                //close and release
                xlWorkbook.Close();
                Marshal.ReleaseComObject(xlWorkbook);

                //quit and release
                xlApp.Quit();
                Marshal.ReleaseComObject(xlApp);
            }
            else
            {
                var fileStream = new FileStream(myFile.FullName, FileMode.Open, FileAccess.Read);

                Console.WriteLine(fileStream);

                using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                {
                    streamReader.ReadLine();

                    while (streamReader.Peek() >= 0)
                    {
                        String line = streamReader.ReadLine();
                        Regex regex = new Regex(",(?=.*\\\")");
                        string match = regex.Replace(line, "_");
                        var Rows = line.Split(new Char[] { ',' }).ToList();
                        if (Rows.Count() < 13 || Rows.Count() > 13) { Console.Write("Error :"); Console.Write("CSV contains " + Rows.Count().ToString() + " Columns"); continue; }
                        Rows = Rows.Select(x => x.ToLower().Trim()).ToList();

                        statusSQL Record = new statusSQL();
                        Record.NodeName = Rows[0].Trim().ToLower();
                        Record.Instance = Rows[1];
                        Record.DBname = Rows[2];
                        Record.LogicalFileName = Rows[3];
                        Record.PathFromSQL = Rows[4];
                        Record.FileType = Rows[5];
                        Record.NodeNameVormetric = Rows[6];
                        Record.GuardPointPath = Rows[7];
                        Record.EncryptionStatus = Rows[8];
                        Record.Application = Rows[9];
                        Record.State = Rows[10];
                        Record.Environment = Rows[11];
                        Record.LastUpdate = Rows[12];

                        Records.Add(Record);
                    }
                }
            }

            using( var db = new DataEntities1())
            {
                int count = 0;
                foreach(statusSQL record in Records){ Console.WriteLine(count);count++; db.statusSQLs.Add(record);}
                try
                {
                   db.SaveChanges();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
               
            }

            List<DatabaseEncryptionLog> DatabaseEncryptionLogs = new List<DatabaseEncryptionLog>();
            foreach (statusSQL record in Records)
            {
                DatabaseEncryptionLog Log = new DatabaseEncryptionLog();

                Log.Application = record.Application;
                Log.EncryptionStatus = record.EncryptionStatus;
                Log.LastUpdate = record.LastUpdate;
                Log.Tally = 1;
                DatabaseEncryptionLogs.Add(Log);
            }
            EqualityComparer EqualityComparer = new EqualityComparer();
            var UniqueLogs =DatabaseEncryptionLogs.Distinct(EqualityComparer).ToList();
            for(var i=0; i<UniqueLogs.Count();i++)
            {
                
                int LogCount=DatabaseEncryptionLogs.Where(x => x.Application == UniqueLogs[i].Application && x.EncryptionStatus==UniqueLogs[i].EncryptionStatus && x.LastUpdate == UniqueLogs[i].LastUpdate).Count();
                UniqueLogs[i].Tally = LogCount;

            }
            using (var db = new DataEntities1())
            {
                foreach (DatabaseEncryptionLog record in UniqueLogs) { db.DatabaseEncryptionLogs.Add(record); }
                try
                {
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }



        }
        class EqualityComparer : IEqualityComparer<DatabaseEncryptionLog>
        {
            public bool Equals(DatabaseEncryptionLog b1, DatabaseEncryptionLog b2)
            {
                if (b2 == null && b1 == null)
                    return true;
                else if (b1 == null | b2 == null)
                    return false;
                else if (b1.Application == b2.Application && b1.EncryptionStatus == b2.EncryptionStatus
                                    && b1.LastUpdate == b2.LastUpdate)
                    return true;
                else
                    return false;
            }
            public int GetHashCode(DatabaseEncryptionLog Log)
            {
                int hCode = Log.Application.GetHashCode() ^ Log.EncryptionStatus.GetHashCode() ^ Log.LastUpdate.GetHashCode();
                return hCode.GetHashCode();
            }
        }
    }
    }
