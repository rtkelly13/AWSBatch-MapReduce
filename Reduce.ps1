$ErrorActionPreference = "Stop"

New-Item -Path "/tmp" -Name "photos" -ItemType "directory"

aws s3 cp s3://map-reduce-demo-bucket/output_images /tmp/photos --recursive 

ls -lR /tmp/photos
ffmpeg -framerate 2 -i '/tmp/photos/output-iteration-%03d.png' -c:v libx264 -pix_fmt yuv420p /tmp/output.mp4

aws s3 cp /tmp/output.mp4 s3://map-reduce-demo-bucket/output.mp4



