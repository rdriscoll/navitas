# Navitas Crestron code

Source code for Crestron AV control systems at Navitas Sydney.\
255 Elizabeth St L 15, Sydney NSW 2000.

Language: Crestron SimplSharp (C# for dotNet 3.5 and 4.7.2)\
Platform: Crestron

## Deploying code

The same code runs in each processor.
On boot the program looks for a file in "//NVRAM//Navitas//" that ends with " system config.json", so load a config file into that directory, e.g., "//NVRAM//Navitas//Navitas 1.01 (1.06) System config.json".

Whean adding a new processor, find a config file that is similar to the one you need and modify that, there is no documentation on the config file so you'll have to figure it out by looking at existing configs.

A processor can be of any type including "RMC3", "RMC4", "CP3", "CP4", "DMPS3-200", "DMPS3-300". A processor will support room joins, there are example config files in the repo.

Each floor has a processor dedicated as a lighting gateway, the processor to be used as a lighting will have it's own IP address entered into the lighting address field of the config, all other processors will use the lighting gateway address in the config.

## Developing code

### 3 series

Solution requires VS2008 + SP1 Professional with Crestron VS plugin.
Good luck getting a copy of VS2008 Professional!

The code in the "3 Series" folder only works on 3 series processors and is what is installed in all rooms as of 2023

### 4 series

Solution requires VS2022 Community Edition.

The code in the "4 series" folder loads without errors, has been tested with a user interface and lighting gateway but has not been tested in production.

### Dependencies

The /dependencies/ folder contains any dlls that may be missing from the build and required to build a project, add references to these libraries if necessary.

## Supported features

* BSS Audio DSP
* Audio via DMPS3-x00
* Dynalite lighting presets
* PIN authorisation
* Video switching via DMPS3-x00, DM Matrix or HD-MD4x1
* Lumens document camera control via RS232
* Projector control via LAN for Panasonic, Hitachi, LG and Epson IWB
* Room combining, up to 4 way room join
* Lecture capture control via Echo360
* PTZ camera control of Panasonic via PTZ
* Dynamic audio and video source and destination menus

## Contributors

Author: Rod Driscoll <rdriscoll@avplus.net.au>