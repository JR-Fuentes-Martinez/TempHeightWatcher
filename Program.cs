﻿using System;
using System.Globalization;
using System.Diagnostics;
using ScottPlot;
using Microsoft.Extensions.Configuration;
using Microsoft.Research.Science.Data;
using Microsoft.Research.Science.Data.Imperative;
using Microsoft.Data.Sqlite;
using ScottPlot.TickGenerators.TimeUnits;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using System.Data;
using System.Xml;

namespace TempHeightWatcher;

class Program
{
    private static DateOnly FInicio = DateOnly.MinValue;
    private static DateOnly FFinal = DateOnly.MinValue;
    private static int SDias = 0;
    private static readonly CultureInfo Usa = new("en-US");
    private static readonly string Directorio = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/Documentos/CDSData";
    private static readonly string ConString = $"Data Source={Directorio}/CdsData.sqlite;";
    private static readonly SqliteConnection DbConn = new(ConString);
    private static readonly float[] Alturas = [1000.0f, 850.0f, 700.0f, 500.0f, 250.0f, 100.0f, 70.0f, 10.0f, 2.0f];

    static async Task Main(string[] args)
    {
        int NError = -1;
        bool DTarea = false, HTarea = false, ITarea = false;
        double Latitud = 0.0, Longitud = 0.0;
        string NomTrabajo = string.Empty;
        /*
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();
        */
        if (args.Length == 3)
        {
            NomTrabajo = args[0];

            if (!int.TryParse(args[1], out SDias)) NError = 1;

            if (args[2].Contains('d', StringComparison.CurrentCultureIgnoreCase))
            {
                DTarea = true;
            }
            if (args[2].Contains('g', StringComparison.CurrentCultureIgnoreCase))
            {
                HTarea = true;
            }
            if (args[2].Contains('i', StringComparison.CurrentCultureIgnoreCase))
            {
                ITarea = true;
                DTarea = false;
                HTarea = false;
            }
            else if (!(DTarea || HTarea)) NError = 2;
        }
        else NError = 0;

        if (NError >= 0)
        {
            if (Thread.CurrentThread.CurrentCulture.Name == "es-ES")
            {
                Console.WriteLine("Debe introducir un Id de trabajo, un entero positivo indicando el número de días a descargas, "
                + "y una cadena incluyendo [d]->Descargar, [g]->Hacer gráfico, o ambas, alternativamente [i]-> Info");
            }
            else
            {
                Console.WriteLine("Enter a Work ID, a positive integer indicating the number of days to download, "
                + "and a string including [d]->Download, [g]->Chart, or both, altertanively [i]->Info");
            }
            return;
        }

        try
        {
            List<DateOnly> Fechas = [];
            DateOnly FirstFecha = DateOnly.MinValue, FinWork = DateOnly.MinValue, LastFecha = DateOnly.MinValue;
            int Estado = 0;

            DbConn.Open();
            using var DbComando = DbConn.CreateCommand();
            DbComando.CommandText = $"SELECT * FROM CdsWorks WHERE [NombreId]='{NomTrabajo}';";
            var ResDb = DbComando.ExecuteReader();

            if (ResDb.HasRows)
            {
                ResDb.Read();
                Latitud = ResDb.GetDouble(2);
                Longitud = ResDb.GetDouble(3);
                FInicio = DateOnly.FromDateTime(ResDb.GetDateTime(0));
                FinWork = DateOnly.FromDateTime(ResDb.GetDateTime(1));
                Estado = ResDb.GetInt32(4);
                LastFecha = DateOnly.FromDateTime(ResDb.GetDateTime(6));
            }
            ResDb.Close();

            if (FInicio == DateOnly.MinValue || Estado == 1)
            {
                if (Estado == 1)
                {
                    Console.WriteLine("El trabajo ya está terminado en la Db.");
                    Console.WriteLine($"Info: Lá ultima fecha descargada es de {LastFecha.ToShortDateString()} para el trabajo {NomTrabajo}");
                }
                else
                {
                    Console.WriteLine("No se ha encontrado el trabajo en la Db.");
                }
                return;
            }

            if (ITarea)
            {
                if (Thread.CurrentThread.CurrentCulture.Name == "es-ES")
                {
                    Console.WriteLine($"Info: Lá ultima fecha descargada es de {LastFecha.ToShortDateString()} para el trabajo {NomTrabajo}");
                    return;
                }
            }
            DbComando.CommandText = $"SELECT DISTINCT Fecha FROM CdsMain WHERE [Latitud]={Latitud.ToString("00.00", Usa)} AND "
                + $"[Longitud] ={Longitud.ToString("00.00", Usa)} ORDER BY Fecha; ";
            ResDb = DbComando.ExecuteReader();

            if (ResDb.HasRows)
            {
                while (ResDb.Read())
                {
                    Fechas.Add(DateOnly.FromDateTime(ResDb.GetDateTime(0)));
                }
            }
            ResDb.Close();
            await ResDb.DisposeAsync();

            if (DTarea)
            {
                using var SScript = File.OpenText("piton/Main_temp.py");
                string SGScript = SScript.ReadToEnd();
                SScript.Close();

                DateOnly FBucle = LastFecha.AddDays(1);
                var StrMeses = string.Empty;
                var StrAños = string.Empty;
                var StrDias = string.Empty;
                var Area = string.Empty;
                StringBuilder StbDias = new(1, 1024);

                FFinal = LastFecha.AddDays(SDias);

                while (FBucle <= FFinal)
                {
                    StrAños = $"{FBucle.Year:0000}";
                    var ElAño = FBucle.Year;

                    while (FBucle.Year == ElAño)
                    {
                        var ElMes = FBucle.Month;
                        StbDias = StbDias.Append('[');
                        StrMeses = $"[\"{FBucle.Month:00}\"]";
                        bool Conmuta = false;

                        while (FBucle.Month == ElMes)
                        {
                            if (!Fechas.Contains(FBucle))
                            {
                                StbDias = StbDias.Append($"\"{FBucle.Day:00}\",");

                                if (!Conmuta)
                                {
                                    FirstFecha = FBucle;
                                    Conmuta = true;
                                }
                            }
                            else
                            {
                                Console.WriteLine($"La fecha {FBucle.ToShortDateString()} ya está en la Db.");
                            }

                            FBucle = FBucle.AddDays(1);
                            if (FBucle > FFinal || FBucle > FinWork)
                            {
                                LastFecha = FBucle.AddDays(-1);
                                break;
                            }
                        }
                        if (StbDias.Length > 1)
                        {
                            StbDias.Remove(StbDias.Length - 1, 1);
                            StbDias = StbDias.Append(']');
                            StrDias = StbDias.ToString().Trim();

                            _ = DescargaDatos(StrDias, StrMeses, StrAños, SGScript, Latitud, Longitud, FirstFecha);
                        }
                        StbDias = StbDias.Clear();

                        if (FBucle > FinWork)
                        {
                            Console.WriteLine("El trabajo de descarga se ha terminado.");
                            DbComando.CommandText = $"UPDATE CdsWorks SET Estado=1,FechaUltima='{FinWork:yyyy-MM-dd}' WHERE [NombreId]='{NomTrabajo}';";
                            DbComando.ExecuteNonQuery();
                            return;
                        }
                        if (FBucle > FFinal)
                        {
                            DbComando.CommandText = $"UPDATE CdsWorks SET FechaUltima='{LastFecha:yyyy-MM-dd}' WHERE [NombreId]='{NomTrabajo}';";
                            DbComando.ExecuteNonQuery();
                            break;
                        }
                    }
                }
            }

            if (HTarea)
            {
                _ = HazGraficos(DateTime.MinValue, 0);
            }

            return;
        }
        catch (SqliteException ex)
        {
            Console.WriteLine(ex.Message);
            return;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return;
        }
        finally
        {
            Console.WriteLine("Programa terminado.");
            if (DbConn.State != System.Data.ConnectionState.Closed) DbConn.Close();
            DbConn?.Dispose();
        }
    }

    static Task DescargaDatos(string Dias, string Meses, string Años, string SGScript, double Latitud, double Longitud, DateOnly Fecha)
    {
        var Cultura = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = Usa;
        int Iterador = 0;
        string RutaT = Path.Combine(Directorio, "tmp/script.py");
        string RutaD = Path.Combine(Directorio, "tmp");

        var FLatitud = Math.Abs(Latitud) - 0.01; // degrees_north ????
        if (Math.Sign(Latitud) < 0) FLatitud *= -1.0;
        var FLongitud = Math.Abs(Longitud) - 0.01;
        if (Math.Sign(Longitud) < 0) FLongitud *= -1.0; //degrees_east ???
        var Area = $"[{Latitud:00.00},{Longitud:00.00},{FLatitud:00.00},{FLongitud:00.00}]";

        try
        {
            while (SGScript.Contains('@'))
            {
                var Indice = SGScript.IndexOf('@');

                if (Indice > -1)
                {
                    SGScript = SGScript.Remove(Indice, 1);

                    if (Iterador == 0) SGScript = SGScript.Insert(Indice, Años);
                    else if (Iterador == 1) SGScript = SGScript.Insert(Indice, Meses);
                    else if (Iterador == 2) SGScript = SGScript.Insert(Indice, Dias);
                    else if (Iterador == 3) SGScript = SGScript.Insert(Indice, Area);

                    Iterador++;
                }
            }
            File.AppendAllText(RutaT, SGScript);

            using Process PPython = new();
            PPython.StartInfo.FileName = "python3";
            PPython.StartInfo.Arguments = $"{RutaT}";
            PPython.StartInfo.WorkingDirectory = RutaD;
            PPython.Start();
            PPython.WaitForExit();
            if (PPython.ExitCode != 0) throw new Exception("Error en el programa python.");

            var Ficheros = Directory.EnumerateFiles(RutaD);
            string RutaNc = string.Empty;
            bool IsNc = false;

            foreach (var item in Ficheros)
            {
                if (item.Contains(".zip"))
                {
                    using var Archivo = ZipFile.OpenRead(item);
                    Archivo.ExtractToDirectory(RutaD);
                    break;
                }
            }
            Ficheros = Directory.EnumerateFiles(RutaD);

            foreach (var item in Ficheros)
            {
                if (item.Contains(".nc"))
                {
                    RutaNc = item;
                    IsNc = true;
                    break;
                }
            }
            if (!IsNc)
            {
                throw new Exception("No se ha podido encontrar el fichero netcdf.");
            }

            DataSet CDSData = DataSet.Open($"msds:nc?file={RutaNc}&openMode=readOnly");
            var Temps = CDSData.GetData<float[,,,]>("t");
            var TransFecha = CDSData.GetData<long[]>("valid_time");
            DateOnly LaFecha = DateOnly.MinValue;
            var JsonTemps = string.Empty;
            IEnumerable<DateOnly> FFechas = [];

            for (int i = 0; i < Temps.GetLength(0); i++) //valid_time - Variable
            {
                LaFecha = Fecha.AddDays((int)TransFecha[i]); //Days from archive time init!!!!!!!!
                FFechas = FFechas.Append(LaFecha);
                IEnumerable<double> TempsByH = [];
                IEnumerable<double> Abscisas = [];

                for (int j = 0; j < Temps.GetLength(1); j++) //Altura - 9 niveles
                {
                    TempsByH = TempsByH.Append(Temps[i, j, 0, 0]); // 1 punto de lat/lon
                }
                for (int n = 0; n < TempsByH.Count(); n++) //Abscisas for spline.
                {
                    Abscisas = Abscisas.Append(n);
                }
                /*
                alglib.spline1dconvdiffcubic(
                    [.. Abscisas], [.. TempsByH], Abscisas.Count(), 1, 0.0, 1, 0.0, [.. Abscisas],
                    Abscisas.Count(), out double[] Valores, out double[] Diffs
                ); //Natural
                */
                if (DbConn.State != System.Data.ConnectionState.Open) DbConn.Open();
                JsonTemps = JsonSerializer.Serialize(TempsByH);
                using var DbCom = DbConn.CreateCommand();
                DbCom.CommandText = "INSERT INTO CdsMain VALUES (@fecha,@valores,@latitud,@longitud,@magnitud);";
                DbCom.Parameters.Clear();
                DbCom.Parameters.AddWithValue("latitud", Latitud);
                DbCom.Parameters.AddWithValue("longitud", Longitud);
                DbCom.Parameters.AddWithValue("fecha", LaFecha);
                DbCom.Parameters.AddWithValue("valores", JsonTemps);
                DbCom.Parameters.AddWithValue("magnitud", "t");
                DbCom.ExecuteNonQuery();
            }

            var options = new JsonReaderOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            };

            foreach (var item in FFechas)
            {
                using var DbCom = DbConn.CreateCommand();
                DbCom.CommandText = "SELECT Valores FROM CdsMain WHERE Fecha=@fecha AND Latitud=@latitud AND Longitud=@longitud AND Magnitud=@magnitud;";
                DbCom.Parameters.Clear();
                DbCom.Parameters.AddWithValue("fecha", item);
                DbCom.Parameters.AddWithValue("latitud", Latitud);
                DbCom.Parameters.AddWithValue("longitud", Longitud);
                DbCom.Parameters.AddWithValue("magnitud", "t");
                using var Resdb = DbCom.ExecuteReader();

                if (Resdb.HasRows)
                {
                    while (Resdb.Read())
                    {
                        byte[] LosBytes = [];
                        Resdb.GetBytes(Resdb.GetString(1), 0, LosBytes, 0, Resdb.GetString(1).Length);
                        var reader = new Utf8JsonReader(LosBytes, options);

                        while (reader.Read())
                        {
                            if (reader.TokenType == JsonTokenType.Number)
                            {

                            }
                        }
                    }
                }
                Resdb.Close(); 


            }
        }
        catch (SqliteException ex)
        {
            Console.WriteLine(ex.Message);
            return Task.FromException(ex);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return Task.FromException(e);
        }
        finally
        {
            foreach (var item in Directory.EnumerateFiles(RutaD))
            {
                File.Delete(item);
            }
            Thread.CurrentThread.CurrentCulture = Cultura;
        }
    }

    static Task HazGraficos(DateTime Fecha, int Dias)
    {
        /*
        DataFrame Data;
        DateTime FFinal = DateTime.MinValue, PFecha;

        Thread.CurrentThread.CurrentCulture = Usa;

        if (File.Exists(ConString))
        {
            Data = DataFrame.LoadCsv(ConString, cultureInfo: Usa);
            PFecha = (DateTime)Data[0, 0];

            if (Fecha != DateTime.MinValue && Dias != 0)
            {
                Fecha.AddHours(PFecha.Hour);
                FFinal = Fecha.AddDays(Dias);
            }
        }
        else
        {
            Console.WriteLine("No se encuentra el fichero de datos en disco.");
            return Task.CompletedTask;
        }

        for (int j = 0; j < 2; j++)
        {
            float Lat;
            float ElAño;

            if (j == 0) Lat = 90.0f;
            else Lat = -90.0f;

            ElAño = PFecha.Year;

            var FInData = Data.Columns["year"].ElementwiseEquals(ElAño);

            if (!FInData.Any())
            {
                Console.WriteLine("No se ha encontrado el año de la fecha introducida");
                break;
            }

            while (FInData.Any())
            {
                var Data3 = Data.Filter(FInData);
                bool Adelante = false;

                if (Fecha != DateTime.MinValue && Dias != 0)
                {
                    var IsFecha = Data3.Columns["time"].ElementwiseGreaterThanOrEqual(Fecha);

                    if (IsFecha.Any())
                    {
                        Data3 = Data3.Filter(IsFecha);
                        Adelante = true;
                    }

                    IsFecha = Data3.Columns["time"].ElementwiseLessThanOrEqual(FFinal);

                    if (IsFecha.Any())
                    {
                        Data3 = Data3.Filter(IsFecha);
                        Adelante = true;
                    }

                    if (!Adelante)
                    {
                        Console.WriteLine("No se ha encontrado registros en las fechas. Mostrando todos los registros.");
                    }
                }

                Data3 = Data3.Filter(Data3.Columns[@"latitude[unit=""degrees_north""]"].ElementwiseEquals(Lat));
                DataFrame DataBucle;
                Multiplot multiplot = new();
                multiplot.AddPlots(Alturas.Length);

                for (int i = 0; i < Alturas.Length; i++)
                {
                    DataBucle = Data3.Filter(Data3.Columns[@"vertCoord[unit=""Pa""]"].ElementwiseEquals(Alturas[i]));
                    IEnumerable<double> Temps = [];
                    IEnumerable<double> Abscisas = [];
                    List<string> Fechas = [];
                    int Indice = 0;

                    foreach (var item in DataBucle.Columns[7])
                    {
                        var LaFecha = (DateTime)Data[Indice, 0];
                        Fechas.Add(LaFecha.ToString("yyyy-MM-dd"));
                        var Numero = (float)item;
                        Temps = Temps.Append((double)Numero);
                        Abscisas = Abscisas.Append(Indice);
                        Indice++;
                    }
                    alglib.spline1dconvdiffcubic([.. Abscisas], [.. Temps], [.. Abscisas], out double[] Valores, out double[] Diffs);

                    //Grafico.HideGrid();
                    var Grafico = multiplot.GetPlot(i);
                    var señal = Grafico.Add.Signal(Diffs);
                    Grafico.YLabel($"{Alturas[i]}Pa");
                    //Grafico.Axes.Bottom.MajorTickStyle.Length = 0;
                    Grafico.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual([.. Abscisas], [.. Fechas]);
                    Grafico.SaveSvg($"/home/jr-fuentes-martinez/Documentos/Phoenix Code/FrontHtml/result_{ElAño}-({Lat}).svg", 1200, 250);
                }
                //multiplot.SavePng($"Graficos/result_{ElAño}-({Lat}).png", 800, 1100);

                //Grafico.SaveSvg($"Graficos/result_{ElAño}-{Lat}.svg", 800, 400);
                if (Fecha != DateTime.MinValue && Dias != 0)
                {
                    Fecha = Fecha.AddYears(-1);
                    FFinal = FFinal.AddYears(-1);
                    PFecha = PFecha.AddYears(-1);
                }
                ElAño--;
                FInData = Data.Columns["year"].ElementwiseEquals(ElAño);
            }
        }
        */
        return Task.CompletedTask;
    }
}




