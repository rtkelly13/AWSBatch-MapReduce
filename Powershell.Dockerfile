FROM mcr.microsoft.com/powershell:lts-debian-11

# Print all output
#COPY . . 
#RUN ls -R

RUN apt-get update && apt-get install -y \
    software-properties-common
RUN apt-get update && apt-get install -y \
    python3.4 \
    python3-pip \
    ffmpeg

RUN pip install awscli

COPY Reduce.ps1 Reduce.ps1

CMD [ "pwsh", "Reduce.ps1" ]