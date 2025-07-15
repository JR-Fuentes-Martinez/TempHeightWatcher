using System;
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
using System.Xml;
using ScottPlot.Plottables;
using ScottPlot.DataSources;
using Microsoft.Data.Analysis;
using SQLitePCL;
using System.Data.Common;
using System.Data.SqlTypes;

namespace TempHeightWatcher;

class Program
{
    private static DateOnly FInicio = DateOnly.MinValue;
    private static DateOnly FFinal = DateOnly.MinValue;
    private static int SDias = 0;
    private static readonly CultureInfo Usa = new("");
    private static readonly string Directorio =
    $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/Documentos/CDSData";
    private static readonly string CDSDataPath = $"{Directorio}/CdsData.csv";
    private static readonly SqliteConnection DbConn = new($"Data Source={Directorio}/CdsData.sqlite;");
    private static DataFrame DFTemp = new();
    private static readonly double[] Alturas = [
        1.0, 2.0, 3.0,
        5.0, 7.0, 10.0,
        20, 30, 50,
        70, 100, 125,
        150, 175, 200,
        225, 250, 300,
        350, 400, 450,
        500, 550, 600,
        650, 700, 750,
        775, 800, 825,
        850, 875, 900,
        925, 950, 975,
        1000
    ];
    internal static readonly double[] values = [0.0];

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
            DateOnly FinWork = DateOnly.MinValue, LastFecha = DateOnly.MinValue, FirstFecha = DateOnly.MinValue;
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


            if (ITarea)
            {
                if (Thread.CurrentThread.CurrentCulture.Name == "es-ES")
                {
                    Console.WriteLine($"Info: Lá ultima fecha descargada es de {LastFecha.ToShortDateString()} para el trabajo {NomTrabajo}");
                    return;
                }
            }

            var MainDF = DataFrame.LoadCsv(CDSDataPath, cultureInfo: Usa);
            DFTemp = MainDF.Filter(MainDF["Latitud"].ElementwiseEquals(Latitud));
            DFTemp = DFTemp.Filter(DFTemp["Longitud"].ElementwiseEquals(Longitud));
            DFTemp = DFTemp.Filter(DFTemp["Magnitud"].ElementwiseEquals("t"));
            DFTemp = DFTemp.Filter(DFTemp["Fecha"].ElementwiseGreaterThanOrEqual(FechaToNum(LastFecha)));
            DFTemp = DFTemp.Filter(DFTemp["Fecha"].ElementwiseLessThanOrEqual(FechaToNum(FinWork)));

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
                            if (!DFTemp.Columns["Fecha"].ElementwiseEquals(FechaToNum(FBucle)).Any())
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
                _ = HazGraficos(FInicio.AddDays(3), 0);
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

    static int FechaToNum(DateOnly Fecha)
    {
        var LAFecha = new DateTime(Fecha, TimeOnly.MinValue);
        var Tiempo = LAFecha - DateTime.UnixEpoch;
        return Tiempo.Days;
    }
    static DateOnly NumToFecha(long Numero)
    {
        return DateOnly.FromDateTime(DateTime.UnixEpoch.AddDays(Numero));
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
            IEnumerable<DateOnly> FFechas = [];

            for (int i = 0; i < Temps.GetLength(0); i++) //valid_time - Variable
            {
                LaFecha = Fecha.AddDays((int)TransFecha[i]); //Days from archive time init!!!!!!!!
                FFechas = FFechas.Append(LaFecha);
                IEnumerable<double> TempsByH = [];

                for (int j = 0; j < Temps.GetLength(1); j++) //Altura - 37 niveles
                {
                    TempsByH = TempsByH.Append(Temps[i, j, 0, 0]); // 1 punto de lat/lon
                }
                alglib.spline1dconvdiffcubic(
                    Alturas, [.. TempsByH], Alturas.Length, 1, 0.0, 1, 0.0, Alturas,
                    Alturas.Length, out double[] Valores, out double[] Diffs
                ); //Natural

                for (int j = 0; j < Temps.GetLength(1); j++) //Altura - 37 niveles
                {
                    var TTempo = TempsByH.ToArray();
                    IEnumerable<object> Fila = [FechaToNum(LaFecha), Latitud, Longitud, 0.0, 0.0, "t", Alturas[j], TTempo[j], Valores[j], Diffs[j]];
                    DFTemp = DFTemp.Append(Fila, true);
                }
            }
            var MainDF = DataFrame.LoadCsv(CDSDataPath, cultureInfo: Usa);
            MainDF = MainDF.Append(DFTemp.Rows, true);
            MainDF = MainDF.OrderBy("Fecha");
            DataFrame.SaveCsv(MainDF, CDSDataPath, cultureInfo: Usa);

            return Task.CompletedTask;
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

    static Task HazGraficos(DateOnly Fecha, int Dias)
    {
        IEnumerable<double> LasDiffs = [];
        DateTime LasFechas = DateTime.MinValue;
        var Data = DataFrame.LoadCsv(CDSDataPath, cultureInfo: Usa);

        Data = Data.Filter(Data["Fecha"].ElementwiseEquals(FechaToNum(Fecha)));
        Data = Data.OrderBy("Nivel");

        foreach (var item in Data.Rows)
        {
            LasDiffs = LasDiffs.Append((float)item[9]);
        }
        double[] Puntos = [LasDiffs.Min(), LasDiffs.Max()];

        Plot myPlot = new();

        // change figure colors
        myPlot.FigureBackground.Color = Color.FromHex("#181818");
        myPlot.DataBackground.Color = Color.FromHex("#1f1f1f");

        // change axis and grid colors
        myPlot.Axes.Color(Color.FromHex("#d7d7d7"));
        myPlot.Grid.MajorLineColor = Color.FromHex("#404040");

        // change legend colors
        myPlot.Legend.BackgroundColor = Color.FromHex("#404040");
        myPlot.Legend.FontColor = Color.FromHex("#d7d7d7");
        myPlot.Legend.OutlineColor = Color.FromHex("#d7d7d7");

        myPlot.Title($"{Fecha.ToShortDateString()}",18);
        myPlot.YLabel("Nivel [hPa]", 16);
        myPlot.XLabel("dT/dp [degK]", 16);

        var sig = myPlot.Add.SignalXY(Alturas, [.. LasDiffs]);
        myPlot.Add.Scatter(xs: Alturas, ys: Puntos);
        myPlot.Axes.SetLimitsY(1000, 0);
        myPlot.Axes.SetLimitsX(-0.5, 0.8);
        sig.Data.Rotated = true;
        // invert the horizontal axis        
        myPlot.SaveSvg($"ComDiffs{Fecha.ToString("yyyyMMdd")}.svg", 350, 800).LaunchFile();

        return Task.CompletedTask;
        /*
        DataFrame Data;
        DateTime FFinal = DateTime.MinValue, PFecha;

        Thread.CurrentThread.CurrentCulture = Usa;

        if (File.Exists(ConString))
        {
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



            ReadOnlySpan<byte> utf8Json = """[0] [0,1] [0,1,1] [0,1,1,2] [0,1,1,2,3]"""u8;
            using var stream = new MemoryStream(utf8Json.ToArray());
            var items = JsonSerializer.DeserializeAsyncEnumerable<double>(stream, topLevelValues: true);


            DateTimeDataFrameColumn DFFecha = new("Fecha", new[] { DateTime.Now });
            DoubleDataFrameColumn DFLatitud = new("Latitud", values);            
            DoubleDataFrameColumn DFLongitud = new("Longitud", values);            
            DoubleDataFrameColumn DFLatStride = new("LatStride", values);            
            DoubleDataFrameColumn DFLonStride = new("LonStride", values);            
            StringDataFrameColumn DFMagnitud = new("Magnitud", ["t"]);            
            DoubleDataFrameColumn DFNivel = new("Nivel", values);            
            DoubleDataFrameColumn DFValor = new("Valor", values);            

            MainDF = new(DFFecha, DFLatitud, DFLongitud, DFLatStride, DFLonStride, DFMagnitud, DFNivel, DFValor);
            DataFrame.SaveCsv(MainDF, CDSDataPath, cultureInfo: Usa);
        */

    }
}




