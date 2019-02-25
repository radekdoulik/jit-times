**jit-times** is a tool to process methods.txt file produced by Xamarin.Android
applications

	Usage: jit-times.exe OPTIONS* <methods-file>

	Processes JIT methods file from XA app with debug.mono.log=timing enabled

	Copyright 2019 Microsoft Corporation

	Options:
	  -h, --help, -?             Show this message and exit
	  -m, --method=TYPE-REGEX    Process only methods whose names match TYPE-REGEX.
	  -s                         Sort by self times. (this is default ordering)
	  -t                         Sort by total times.
	  -u                         Show unsorted results.
	  -v, --verbose              Output information about progress during the run
	                               of the tool

### Getting the `methods.txt` file

The `methods.txt` file can be aquired from Xamarin.Android application like this:

* Enable `debug.mono.log`

	`adb shell setprop debug.mono.log timing`

* Run the application

* Collect the file - replace the `${PACKAGE}` with your application package name

	`adb shell run-as ${PACKAGE} cat /data/data/${PACKAGE}/files/.__override__/methods.txt > methods.txt`
