import cdsapi

dataset = "derived-era5-pressure-levels-daily-statistics"
request = {
    "product_type": "reanalysis",
    "variable": [
        #"ozone_mass_mixing_ratio",
        #"relative_humidity",
        #"specific_humidity",
        "temperature"
    ],
    "year": "@",    #2025
    "month": @,     #["06"]
    "day": @,       #["12"]
    "pressure_level": [
        "2", "10", "20",
        "100", "250", "500",
        "700", "850", "1000"
    ],
    "daily_statistic": "daily_mean",
    "time_zone": "utc+00:00",
    "frequency": "6_hourly",
    "area": @,
    "data_format": "netcdf",
    "download_format": "unarchived"
}
client = cdsapi.Client()
client.retrieve(dataset, request).download()
