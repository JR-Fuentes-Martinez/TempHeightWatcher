using System;
using System.Globalization;
using Microsoft.Data.Analysis;
using System.Diagnostics;
using ScottPlot;
using Microsoft.Extensions.Configuration;
using Microsoft.Research.Science.Data;
using Microsoft.Research.Science.Data.Imperative;
using Microsoft.Research.Science.Data.NetCDF4;
using Microsoft.Research.Science.Data.CSV;
using Microsoft.Data.Sqlite;
using System.ComponentModel;


namespace TempHeightWatcher;

class Program
{
    private static DateTime FInicio = DateTime.MinValue;
    private static DateOnly FFinal = DateOnly.MinValue;
    private static int SDias = 0;
    private static readonly CultureInfo Usa = new("en-US");
    private static readonly string Directorio = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/Documentos/CDSData";
    private static readonly string ConString = $"Data Source={Directorio}/CdsData.sqlite;";
    private static readonly SqliteConnection DbConn = new(ConString);
    private static readonly float[] Alturas = [100000.0f, 85000.0f, 70000.0f, 50000.0f, 25000.0f, 10000.0f, 7000.0f, 1000.0f];
    private static double HFromFich = 0.0;
    private static int NumAños = 4;

    static void Main(string[] args)
    {
        int NError = -1;
        bool DTarea = false, HTarea = false;

        IConfigurationRoot config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();

        if (args.Length > 1)
        {
            if (!DateTime.TryParse(args[0], out FInicio))
            {
                NError = 1;
            }
            if (!int.TryParse(args[1], out SDias))
            {
                NError = 2;
            }
            if (NError == -1 && args.Length == 3)
            {
                NError = 4;

                if (args[2].Contains('d', StringComparison.CurrentCultureIgnoreCase))
                {
                    DTarea = true;
                    NError = -1;
                }
                if (args[2].Contains('g', StringComparison.CurrentCultureIgnoreCase))
                {
                    HTarea = true;
                    NError = -1;
                }
            }
            else NError = 3;
        }
        else NError = 0;

        if (NError >= 0)
        {
            Console.WriteLine("Debe introducir una fecha en formato \"yyyy-MM-dd 00:00\", un entero indicando el número de días "
                + "y una cadena incluyendo [d]->Descargar, [g]->Hacer gráfico, o ambas.");
            return;
        }

        try
        {
            bool Adelante = true;
            FFinal = DateOnly.FromDateTime(FInicio.AddDays(SDias));
            DateOnly FBucle = DateOnly.FromDateTime(FInicio);
            List<DateOnly> Fechas = [];

            DbConn.Open();
            using var DbComando = DbConn.CreateCommand();
            DbComando.CommandText = $"SELECT Fecha FROM CdsMain ORDER BY Fecha; ";
            using var ResDb = DbComando.ExecuteReader();

            if (ResDb.HasRows)
            {
                while (ResDb.Read())
                {
                    Fechas.Add(DateOnly.FromDateTime(ResDb.GetDateTime(0)));
                }
            }
            ResDb.Close();

            if (DTarea)
            {
                if (!DescargaDatos(config, Fechas, FInicio, SDias, NumAños).IsCompletedSuccessfully)
                {
                    Adelante = false;
                }
            }

            if (HTarea && Adelante)
            {
                _ = HazGraficos(DateTime.MinValue, 0);
            }
            else
            {
                if (!Adelante) Console.WriteLine("La tarea de descarga no termino adecuadamente.");
            }

            Console.WriteLine("Programa terminado.");
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
    }

    static Task DescargaDatos(IConfigurationRoot config, List<DateOnly> Fechas, DateTime Fecha, int Dias, int NumAños)
    {
        var DInicio = DateOnly.FromDateTime(Fecha);
        Thread.CurrentThread.CurrentCulture = Usa;
        float Lat, Long = 0.0f;
        string Meses = string.Empty;
        string SDias = string.Empty;
        int AntMes, AntAño;

        AntAño = DInicio.Year;
        AntMes = DInicio.Month;

        for (int i = 0; i < Dias; i++)
        {
            FormatFecha Ff = new(DInicio);
            if (DInicio.Year == AntAño)
            {
                if (DInicio.Month == AntMes)
                {
                    
                }
            }
                


            DInicio.AddDays(1);
        }


        using var SScript = File.OpenText("/piton/Main.py");
        string SGScript = SScript.ReadToEnd();
        SScript.Close();

        Lat = 90.0f;
        int Iterador = 0;

        while (SGScript.Contains('@'))
        {
            FormatFecha LaFecha = new(Fecha);

            var Indice = SGScript.IndexOf('@');

            if (Indice > -1)
            {
                SGScript = SGScript.Remove(Indice, 1);

                if (Iterador == 0) SGScript = SGScript.Insert(Indice, Año);
                if (Iterador == 1) SGScript = SGScript.Insert(Indice, $"['{Mes}]'");
                if (Iterador == 2)
                {

                }
                Iterador++;
            }

        }

        var Cadena = $@"https://www.ncei.noaa.gov/thredds/ncss/model-gfs-g3-anl-files/{Año}{Mes}/{Año}{Mes}{Dia}/gfs_3_{Año}{Mes}{Dia}_0600_006.grb2?var=Ozone_Mixing_Ratio_isobaric&var=Relative_humidity_isobaric&var=Specific_humidity_isobaric&var=Temperature_isobaric&temporal=all&latitude={Lat}&longitude={Long}&accept=CSV";
        var UserAgent = $"TempHeightWatcher-wget-net8.0;1.0-alpha;{config["GUID"]}";

        try
        {
            using Process PPython = new();
            PPython.StartInfo.FileName = "python3";
            PPython.StartInfo.Arguments = $" -O {Directorio}/temp/temp1fff.tmp --user-agent={UserAgent} {Cadena}";
            PPython.Start();
            PPython.WaitForExit();
            if (PPython.ExitCode != 0) throw new Exception("Error en el programa python.");
        }
        catch (System.Exception e)
        {
            Console.WriteLine(e.Message);
            return Task.FromException(e);
        }
        DataFrame Data1 = DataFrame.LoadCsv($"{Directorio}/temp/temp1fff.tmp", cultureInfo: Usa);
        if (File.Exists($"{Directorio}/temp/temp1fff.tmp")) File.Delete($"{Directorio}/temp/temp1fff.tmp");

        Thread.Sleep(2000);

        Lat = -90.0f;
        //Cadena = $@"https://www.ncei.noaa.gov/thredds/ncss/model-gfs-g3-anl-files/{Año}{Mes}/{Año}{Mes}{Dia}/gfs_3_{Año}{Mes}{Dia}_0600_006.grb2?var=Ozone_Mixing_Ratio_isobaric&var=Relative_humidity_isobaric&var=Specific_humidity_isobaric&var=Temperature_isobaric&disableLLSubset=on&disableProjSubset=on&horizStride=1&temporal=all&timeStride=1&req=station&latitude={Lat}&longitude={Long}&accept=CSV";
        Cadena = $@"https://www.ncei.noaa.gov/thredds/ncss/model-gfs-g3-anl-files/{Año}{Mes}/{Año}{Mes}{Dia}/gfs_3_{Año}{Mes}{Dia}_0600_006.grb2?var=Ozone_Mixing_Ratio_isobaric&var=Relative_humidity_isobaric&var=Specific_humidity_isobaric&var=Temperature_isobaric&temporal=all&latitude={Lat}&longitude={Long}&accept=CSV";

        try
        {
            using Process WGet = new();
            WGet.StartInfo.FileName = "wget";
            WGet.StartInfo.Arguments = $" -O {Directorio}/temp/temp1fff.tmp --user-agent={UserAgent} {Cadena}";
            WGet.Start();
            WGet.WaitForExit();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return Task.FromException(e);
        }
        DataFrame Data2 = DataFrame.LoadCsv($"{Directorio}/temp/temp1fff.tmp", cultureInfo: Usa);
        if (File.Exists($"{Directorio}/temp/temp1fff.tmp")) File.Delete($"{Directorio}/temp/temp1fff.tmp");

        Console.WriteLine($"Descargados archivos de fecha {FBucle.ToShortDateString()}");

        for (int i = 0; i < Data1.Rows.Count; i++)
        {
            for (int j = 0; j < Alturas.Length; j++)
            {
                if ((float)Data1[i, 3] == Alturas[j])
                {
                    Data = Data.Append(Data1.Rows[i], inPlace: true);
                }
            }
        }
        for (int i = 0; i < Data2.Rows.Count; i++)
        {
            for (int j = 0; j < Alturas.Length; j++)
            {
                if ((float)Data2[i, 3] == Alturas[j])
                {
                    Data = Data.Append(Data2.Rows[i], inPlace: true);
                }
            }
        }
        Thread.Sleep(2000);

        FBucle = FBucle.AddYears(-1);


        if (!PasaDeLargo)
        {
            Data = Data.OrderByDescending("time");

            for (int i = 0; i < Data.Rows.Count; i++)
            {
                var Ffecha = (DateTime)Data[i, 0];
                Data[i, 8] = (float)Ffecha.Year;
            }
            if (File.Exists($"{ConString}.old")) File.Delete($"{ConString}.old");
            File.Copy(ConString, $"{ConString}.old");
            DataFrame.SaveCsv(Data, ConString, cultureInfo: Usa);
        }
        Console.WriteLine($"{Data.Rows.Count} en la Db.");
        */
            return Task.CompletedTask;
    }

    static Task HazGraficos(DateTime Fecha, int Dias)
    {
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
        return Task.CompletedTask;
    }

    public class FormatFecha
    {
        private readonly DateOnly _Fecha = DateOnly.MinValue;

        public FormatFecha(DateOnly Fecha)
        {
            _Fecha = Fecha;
            FAño = _Fecha.Year.ToString("0000");
            FMes = Fecha.Month.ToString("00");
            FDia = Fecha.Day.ToString("00");
        }
        public string FAño { get; private set; }
        public string FMes { get; private set; }
        public string FDia { get; private set; }
    }
}




