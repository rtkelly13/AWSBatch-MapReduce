import holoviews as hv, datashader as ds, pandas as pd, colorcet as cc, numpy as np
from datashader.utils import export_image
from pyproj import Transformer
import json
import os
import boto3
import platform

hv.extension('bokeh')

profile_name = os.environ.get("AWS_PROFILE")
if profile_name is not None:
    boto3.setup_default_session(profile_name=profile_name)

s3_client = boto3.client('s3')

TRAN_4326_TO_3857 = Transformer.from_crs("EPSG:4326", "EPSG:3857")
bucket_name = "map-reduce-demo-bucket"


def jobs_to_run():
    content_object = s3_client.get_object(Bucket=bucket_name, Key="jobData.json")
    file_content = content_object['Body'].read()
    data = json.loads(file_content)

    if data is None:
        raise Exception("json must have at least some value")

    if not isinstance(data, list):
        raise Exception("json must be a list")

    for iteration, element in enumerate(data):
        element['Iteration'] = iteration

    batch_index = os.environ.get('AWS_BATCH_JOB_ARRAY_INDEX')

    if batch_index is None:
        return data

    index = int(batch_index)

    data_len = len(data)
    if index >= data_len:
        raise Exception(f"job json has too few values expected element {index} out of {data_len}")

    return [data[index]]


elements = jobs_to_run()

for taxi_data in elements:
    file_url = taxi_data['FileUrl']
    i = taxi_data['Iteration']
    print(i)
    print(file_url)
    df = pd.read_parquet(file_url, engine='fastparquet')

    lat_field = 'End_Lat'
    long_field = 'End_Lon'
    if 'dropoff_latitude' in df:
        lat_field = 'dropoff_latitude'
        long_field = 'dropoff_longitude'

    if not lat_field in df:
        raise Exception(f"unable to find lat_field {lat_field} out of {df.columns}")

    dropoff_x, dropoff_y = TRAN_4326_TO_3857.transform(
        df[lat_field].to_numpy(),
        df[long_field].to_numpy())

    df['dropoff_x'] = dropoff_x
    df['dropoff_y'] = dropoff_y
    filt = (df['dropoff_x'] >= -8254332.0) & (df['dropoff_x'] <= -8209813.5) & \
           (4965255.5 <= df['dropoff_y']) & (4988769.5 >= df['dropoff_y'])
    df = df.loc[filt]
    canvas = ds.Canvas(plot_width=1400, plot_height=1000)
    agg = canvas.points(df, 'dropoff_x', 'dropoff_y')
    shaded = ds.tf.shade(agg, cmap=cc.fire)

    exportPath = "../../data/results"
    curr_platform = platform.system()
    if curr_platform != 'Windows':
        exportPath = "/tmp"
    file_name = f"output-iteration-{i:03d}"
    export_image(shaded, file_name, background="black", export_path=exportPath)

    full_path = f"{exportPath}/{file_name}.png"

    response = s3_client.upload_file(full_path, bucket_name, f"output_images/{file_name}.png")

    print(full_path)
    print(f"finished - {i}")
