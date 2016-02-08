$archname = $args[0]
$archpath = $args[1]

# FTP Settings
echo "Uploading zip to ftp:"
echo $archname
echo $archpath
$ftpHost = "ftp://Omnicom:Greenwich@ftp.codeworldwide.com/"
$ftpDirectory = "ClientLink_3.0/Packages/"
$ftpPath = $ftpHost + $ftpDirectory + $archname;

echo $ftpPath
             
# Upload process
$webclient = New-Object System.Net.WebClient
$uri = New-Object System.Uri($ftpPath)
$webclient.UploadFile($uri, $archpath)
