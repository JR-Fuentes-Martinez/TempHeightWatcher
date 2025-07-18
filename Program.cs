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
    private const int NumDataSignalsForDataframes = 2;  //temperature, specific humidity
    private static int SDias = 0;
    private static readonly CultureInfo Usa = new("");
    private static readonly string Directorio =
    $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/Documentos/CDSData";
    private static readonly string CDSDataPath = $"{Directorio}/CdsData.csv";
    private static readonly SqliteConnection DbConn = new($"Data Source={Directorio}/CdsData.sqlite;");
    private static readonly DataFrame[] DFTemps = new DataFrame[NumDataSignalsForDataframes];
    private static double[] Alturas = [
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

    static void Main(string[] args)
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
            var DFTemp = MainDF.Filter(MainDF["Latitud"].ElementwiseEquals(Latitud));
            DFTemp = DFTemp.Filter(DFTemp["Longitud"].ElementwiseEquals(Longitud));
            DFTemp = DFTemp.Filter(DFTemp["Fecha"].ElementwiseGreaterThanOrEqual(FechaToNum(FInicio)));
            DFTemp = DFTemp.Filter(DFTemp["Fecha"].ElementwiseLessThanOrEqual(FechaToNum(FinWork)));

            if (DTarea)
            {
                bool Terminado = false;

                for (int q = 0; q < NumDataSignalsForDataframes; q++)
                {
                    string NScript = string.Empty;
                    string NMagnitud = string.Empty;
                    Terminado = false;

                    if (q == 0)
                    {
                        NScript = "piton/Main_temp.py";
                        NMagnitud = "t";
                    }
                    else
                    {
                        NScript = "piton/Main_sphum.py";
                        NMagnitud = "q";
                    }

                    DFTemps[q] = DFTemp.Filter(DFTemp["Magnitud"].ElementwiseEquals(NMagnitud));

                    using var SScript = File.OpenText(NScript);
                    string SGScript = SScript.ReadToEnd();
                    SScript.Close();

                    DateOnly FBucle = FInicio.AddDays(1);
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
                    if (FFinal > FinWork) FFinal = FinWork;

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
                                    Console.WriteLine($"La fecha {FBucle.ToShortDateString()} para {NMagnitud} ya está en la Db.");
                                }
                                FBucle = FBucle.AddDays(1);

                                if (FBucle > FFinal || FBucle > FinWork)
                                {
                                    if (q == 1) LastFecha = FBucle.AddDays(-1);
                                    break;
                                }
                            }

                            if (StbDias.Length > 1)
                            {
                                StbDias.Remove(StbDias.Length - 1, 1);
                                StbDias = StbDias.Append(']');
                                StrDias = StbDias.ToString().Trim();

                                _ = DescargaDatos(StrDias, StrMeses, StrAños, SGScript, Latitud, Longitud, FirstFecha, NMagnitud, q);
                            }
                            StbDias = StbDias.Clear();

                            if (FBucle > FinWork || FBucle > FFinal) break;
                        }
                    }
                    if (FBucle > FinWork) Terminado = true;
                }

                if (Terminado)
                {
                    Console.WriteLine("El trabajo de descarga se ha terminado.");
                    DbComando.CommandText = $"UPDATE CdsWorks SET Estado=1,FechaUltima='{FinWork:yyyy-MM-dd}' WHERE [NombreId]='{NomTrabajo}';";
                }
                else
                {
                    DbComando.CommandText = $"UPDATE CdsWorks SET FechaUltima='{LastFecha:yyyy-MM-dd}' WHERE [NombreId]='{NomTrabajo}';";
                }
                DbComando.ExecuteNonQuery();
            }

            if (HTarea)
            {
                _ = HazGraficos(FInicio.AddDays(1), Latitud, Longitud, ["t", "q"], ["dT/dp [degK]", "dq/dp [g/Kg]"]);
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

    static Task DescargaDatos(
        string Dias, string Meses, string Años, string SGScript, double Latitud,
         double Longitud, DateOnly Fecha, string Magnitud, int IDDframe)
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
            var Temps = CDSData.GetData<float[,,,]>(Magnitud);
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
                    IEnumerable<object> Fila = [FechaToNum(LaFecha), Latitud, Longitud, 0.0, 0.0, Magnitud, Alturas[j], TTempo[j], Valores[j], Diffs[j]];
                    DFTemps[IDDframe] = DFTemps[IDDframe].Append(Fila, true);
                }
            }
            var MainDF = DataFrame.LoadCsv(CDSDataPath, cultureInfo: Usa);
            MainDF = MainDF.Append(DFTemps[IDDframe].Rows, true);
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

    static Task HazGraficos(DateOnly Fecha, double Latitud, double Longitud, string[] Magnitudes, string[] Unidades)
    {
        var Cultura = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = Usa;        
        
        var Data = DataFrame.LoadCsv(CDSDataPath, cultureInfo: Usa);
        var LasDiffs = new IEnumerable<double>[Magnitudes.Length];

        for (int i = 0; i < LasDiffs.Length; i++)
        {
            LasDiffs[i] = [];
        }        
        var Datas = new DataFrame[Magnitudes.Length];
        float Minimo = float.NaN, Maximo = float.NaN, HMinimo = float.NaN, HMaximo = float.NaN;

        Data = Data.Filter(Data["Fecha"].ElementwiseEquals(FechaToNum(Fecha)));
        Data = Data.OrderBy("Nivel");
        int Multiplo;

        for (int i = 0; i < Magnitudes.Length; i++)
        {
            Datas[i] = Data.Filter(Data["Magnitud"].ElementwiseEquals(Magnitudes[i]));

            foreach (var item in Datas[i].Rows)
            {
                if (Magnitudes[i].Contains('q', StringComparison.CurrentCultureIgnoreCase)) Multiplo = 1000;
                else Multiplo = 1;

                LasDiffs[i] = LasDiffs[i].Append((float)item[9] * Multiplo);

                if (Magnitudes[i].Contains('t', StringComparison.CurrentCultureIgnoreCase))
                {
                    var ElValor = (float)item[7];
                    var Altura = (float)item[6];

                    if (float.IsNaN(Minimo)) Minimo = ElValor;
                    if (float.IsNaN(Maximo)) Maximo = ElValor;

                    if (ElValor < Minimo)
                    {
                        Minimo = ElValor;
                        HMinimo = Altura;
                    }
                    if (ElValor > Maximo)
                    {
                        Maximo = ElValor;
                        HMaximo = Altura;
                    }
                }
            }
            LasDiffs[i] = LasDiffs[i].Skip(10);
        }
        Alturas = [.. Alturas.Skip(10)];

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

        myPlot.Title($"{Fecha.ToLongDateString()}  Lat:{Latitud:00.00} Lon:{Longitud:00.00}", 16);
        myPlot.XLabel("Pressure Level [hPa]", 16);
        myPlot.YLabel("dT/dp [degK]", 16);
        myPlot.Add.ScatterLine(Alturas, [.. LasDiffs[0]], Generate.RandomColor());

        for (int i = 1; i < LasDiffs.Length; i++)
        {
            var Eje = myPlot.Axes.AddLeftAxis();
            Eje.LabelText = Unidades[i];
            Eje.Color(Color.FromHex("#d7d7d7"));
            Eje.LabelFontSize = 16;
            var ff = myPlot.Add.ScatterLine(Alturas, [.. LasDiffs[i]], Generate.RandomColor());
            ff.Axes.YAxis = Eje;
        }        
        //sig.Data.Rotated = true;
        var linea1 = myPlot.Add.VerticalLine(HMinimo, 3, Colors.DarkBlue);
        linea1.Text = "COLD";
        linea1.LabelOppositeAxis = true;
        linea1.LabelFontSize = 14;
        linea1.LabelOffsetY = 0;
        var linea2 = myPlot.Add.VerticalLine(HMaximo, 3, Colors.Red);
        linea2.Text = "HOT";
        linea2.LabelOppositeAxis = true;
        linea2.LabelFontSize = 14;
        linea2.LabelOffsetY = 0;

        myPlot.Axes.Top.MinimumSize = 30;
        myPlot.Axes.Right.MinimumSize = 30;
        myPlot.Axes.Left.MinimumSize = 30;

        //myPlot.Add.Scatter(xs: Alturas, ys: Puntos);
        myPlot.Axes.SetLimitsX(100, 1013);
        //myPlot.Axes.SetLimitsY(-0.5, 0.8);
        myPlot.SaveSvg($"ComDiffs{Fecha.ToString("yyyyMMdd")}.svg", 950, 350);

        Thread.CurrentThread.CurrentCulture = Cultura;

        return Task.CompletedTask;        

        /*
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




