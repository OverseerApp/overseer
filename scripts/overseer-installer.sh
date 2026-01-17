#!/bin/bash

overseerVersion='2.0.0-alpha.1'
dotnetPath=${PWD}'/.dotnet'
dotnetExecPath=${dotnetPath}'/dotnet'
overseerDirectory=${PWD}'/overseer'
overseerExecutable='Overseer.Server.dll'
overseerExecutablePath=${overseerDirectory}'/'${overseerExecutable}
overseerPID=$(ps auxf | grep ${overseerExecutable} | grep -v grep  | awk '{print $2}')
overseerZipFile=overseer_server_${overseerVersion}.zip
overseerZipUrl=https://github.com/michaelfdeberry/overseer/releases/download/${overseerVersion}/${overseerZipFile}
servicePath='/lib/systemd/system/overseer.service'

echo Installing Overseer...

# update the system
apt-get update

# install the prerequisites  

if [ ! -f "$dotnetPath" ]
then
    echo ".NET is not installed. Installing .NET..."
    # Install .NET runtime dependencies
    apt-get install -y libc6 libgcc-s1 libgssapi-krb5-2 libstdc++6 zlib1g
    
    # Install ICU libraries (try multiple versions for compatibility)
    apt-get install -y libicu-dev || apt-get install -y libicu72 || apt-get install -y libicu70 || apt-get install -y libicu67
    
    # Install SSL libraries (try multiple versions for compatibility)
    apt-get install -y libssl-dev || apt-get install -y libssl3 || apt-get install -y libssl1.1
    
    # Install dependencies for Emgu.CV (OpenCV)
    echo "Installing Emgu.CV dependencies..."
    apt-get install -y \
        libopencv-dev \
        libgdiplus \
        libc6-dev
    
    # Install additional libraries that may be needed by OpenCV
    apt-get install -y \
        libgtk2.0-dev \
        pkg-config \
        libavcodec-dev \
        libavformat-dev \
        libswscale-dev
    
    wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh 
    chmod +x ./dotnet-install.sh 
    ./dotnet-install.sh --version latest --runtime aspnetcore --install-dir ${dotnetPath}
else
    echo ".NET is already installed. Skipping .NET installation."
fi
 
# check if the overseer directory exists, if not create it
if [ ! -d "$overseerDirectory" ]; then
    mkdir -p $overseerDirectory
    echo "Created directory $overseerDirectory"
fi

# download the latest and unzip the archive
wget $overseerZipUrl
unzip -o ${overseerZipFile} -d ${overseerDirectory} 
rm ${overseerZipFile}

if [ -n "${overseerPID}" ]; then
    # stop the service if it's is running
    service overseer stop
    
    # check if the process is still running and send the kill command if it is
    # This is needed in case it was started manually
    if ps -p ${overseerPID} > /dev/null; then
        kill ${overseerPID}
    fi
fi

# create or overwrite the service file
> $servicePath
echo [Unit] >> $servicePath
echo Description=Overseer Daemon >> $servicePath
echo >> $servicePath
echo [Service] >> $servicePath
echo WorkingDirectory=${overseerDirectory} >> $servicePath
echo ExecStart=${dotnetExecPath} ${overseerExecutablePath} >> $servicePath
echo Restart=always >> $servicePath
echo RestartSec=10 >> $servicePath
echo KillSignal=SIGINT >> $servicePath
echo SyslogIdentifier=overseer >> $servicePath 
echo Environment=ASPNETCORE_ENVIRONMENT=Production >> $servicePath
echo Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false >> $servicePath
echo >> $servicePath
echo [Install] >> $servicePath
echo WantedBy=multi-user.target >> $servicePath
echo >> $servicePath

# enable the service
systemctl enable overseer

# start the service
service overseer start

if [ ! -f /etc/nginx/sites-available/overseer ]; then
    read -p "Do you want to install a reverse proxy with Nginx? (y/n): " installNginx
else
    echo "Nginx configuration for Overseer already exists. Skipping prompt."
    installNginx="n"
fi

if [ "$installNginx" == "y" ]; then
    # install nginx
    if ! command -v nginx &> /dev/null
    then
        echo "Nginx is not installed. Installing Nginx..."
        apt-get install nginx
    else
        echo "Nginx is already installed. Skipping Nginx installation."
    fi

    read -p "Enter the external port for Nginx (default is 80): " externalPort
    externalPort=${externalPort:-80}
    
    # create a new nginx configuration file
    nginxConfigPath='/etc/nginx/sites-available/overseer'
    > $nginxConfigPath
    echo server { >> $nginxConfigPath
    echo '    listen ' ${externalPort}';' >> $nginxConfigPath 
    echo '    location / {' >> $nginxConfigPath
    echo '        proxy_pass http://localhost:9000;' >> $nginxConfigPath
    echo '        proxy_http_version 1.1;' >> $nginxConfigPath
    echo '        proxy_cache_bypass $http_upgrade;' >> $nginxConfigPath
    echo '        proxy_set_header Upgrade $http_upgrade;' >> $nginxConfigPath
    echo '        proxy_set_header Connection keep-alive;' >> $nginxConfigPath
    echo '        proxy_set_header X-Forwarded-Proto $scheme;' >> $nginxConfigPath
    echo '        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;' >> $nginxConfigPath
    echo '        proxy_set_header Host $host;' >> $nginxConfigPath
    echo '    }' >> $nginxConfigPath
    echo '' >> $nginxConfigPath
    echo '    location /push {' >> $nginxConfigPath
    echo '        proxy_pass              http://localhost:9000/push;' >> $nginxConfigPath
    echo '        proxy_http_version      1.1;' >> $nginxConfigPath
    echo '        proxy_cache_bypass      $http_upgrade;' >> $nginxConfigPath
    echo '        proxy_set_header        Upgrade $http_upgrade;' >> $nginxConfigPath
    echo '        proxy_set_header        Connection "upgrade";' >> $nginxConfigPath
    echo '        proxy_set_header        X-Forwarded-For $proxy_add_x_forwarded_for;' >> $nginxConfigPath
    echo '        proxy_set_header        X-Forwarded-Proto $scheme;' >> $nginxConfigPath
    echo '        proxy_set_header        Host $host;' >> $nginxConfigPath
    echo '    }' >> $nginxConfigPath
    echo } >> $nginxConfigPath

    # enable the new configuration by creating a symlink
    ln -s $nginxConfigPath /etc/nginx/sites-enabled/
    # remove the default configuration to disable it
    rm /etc/nginx/sites-enabled/default

    # test the nginx configuration
    nginx -t

    # restart nginx to apply the changes
    systemctl restart nginx
    echo ""
    echo "-----------------------------------------------------------------------------------------------------"
    echo ""
    echo "Nginx has been installed and configured as a reverse proxy."
    echo "This only provides a basic configuration. You may need to make additional changes to suit your needs."
else
    echo "Nginx installation skipped."
fi

echo Overseer installation has completed.
