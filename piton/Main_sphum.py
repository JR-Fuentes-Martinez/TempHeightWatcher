import cdsapi

dataset = "derived-era5-pressure-levels-daily-statistics"
request = {
    "product_type": "reanalysis",
    "variable": [        
        "specific_humidity"
    ],
    "year": "@",
    "month": @,
    "day": @,
    "pressure_level": [
    "1", "2", "3",
    "5", "7", "10",
    "20", "30", "50",
    "70", "100", "125",
    "150", "175", "200",
    "225", "250", "300",
    "350", "400", "450",
    "500", "550", "600",
    "650", "700", "750",
    "775", "800", "825",
    "850", "875", "900",
    "925", "950", "975",
    "1000"
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
