This program works with print systems that use the Windows account associated with a queued print job to assign print jobs to users

To use the program:

 * Extract the program and its supporting files into a directory on the print server.
 * Install Ghostscript on the print server (if it's not already installed).
 * Edit the TSVCEO.CloudPrint.exe.config file
 * The CloudPrintAcceptDomains setting will need to be changed - it is a comma-delimited list of accepted Google domains:
   - Replace gmail.com with your Google Apps domain name
 * If you use a domain to access the internet, uncomment the WebProxyHost and WebProxyPort settings and edit them as necessary.
   - Replace proxy.example.com with your proxy hostname
   - Replace 3128 with your proxy port
 * Save the above file.
 * Run the InstallCloudPrint.cmd script as an administrator.
 * Do not delete the directory - it is where the cloud print proxy service will run from.

If you haven't registered the print proxy on this print server:

 * In a web browser, go to http://<hostname>:12387/
 * Log in as a domain user (e.g. cedt-admin)
 * Click the "Register the print proxy" link
 * Click the "Register the Proxy" button
 * This should bring up a page with a link to claim the proxy.  Click the link, and it should open in a new tab or window.
 * Log into Google Apps if you haven't already logged in.
 * Click Finish printer registration
 * Go back to the proxy registration tab or window
 * Enter the Google email address under which you registered the printer and click "Get the Authorization Code"
 * If registration was successful, you should see "Print proxy successfully registered".
 * The printer should now appear in your printers list.

For a user to have their print jobs print:

 * In a web browser, go to http://<hostname>:12387/
 * Log in as the domain user
 * Their print jobs should now start printing.
