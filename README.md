# LAP - Linux Artifact Parser

LAP is a collection of tools in one GUI software that parse the most important Linux artifacts, making their review faster.
LAP aims to analyse Linux artifacts collected with DFIR tools like Velociraptor, GRR etc. 

# Overview

The software consists in several tabs, each of them dedicated to a given artifact category.
The tabs of the GUI are as follows:

1) **Log Parsers** - parses the most common Linux logs and returns them in a CSV file easy to filter out with Excel
2) **ELF File Analysis** - Extracts strings, metadata and imported functions from an ELF executable. This also does some basic anomaly checks in the ELF sections
3) **/Etc/Shadow** - a simple /etc/shadow parser
4) **/Etc/Passwd** - a simple /etc/passwd parser
5) **/Proc/Modules** - reads the /proc/modules artifact and interprets its flags (if a module tainted the Kernel, if it is not part of the official Kernel etc.)
6) **Proc Reconstruct** - examines the /proc virtual filesystem and returns important info on memory in an easy to review GUI
7) **Crawlers** - traverses the entire artifact collection, parses several artifact types and returns the parse output in consolidated views 
8) **RPM Package Analysis** - examines an RPM package and extracts scripts, metadata, hashes etc.
9) **DEB Package Analysis** - examines a DEB package and extracts scripts, metadata, hashes etc.
10) **SQLite Extractors** - extracts tables from some Linux artifacts in SQLite format
11) **Tree View** - The entire collection can be viewed in tree mode with a built-in "Notepad++ like" editor and possibility to dump data in binary format

# Screenshots
&nbsp;&nbsp;
**LOG PARSERS**

<img width="895" height="285" alt="Logparsers" src="https://github.com/user-attachments/assets/75e5901e-a190-4346-83dd-7325070fba4b" />
&nbsp;
&nbsp;

**ELF FILE ANALYSIS**


<img width="1232" height="348" alt="elf1" src="https://github.com/user-attachments/assets/831ed6ba-b668-4d84-b966-a8c748f40755" />
&nbsp;
<img width="1012" height="465" alt="ELF2" src="https://github.com/user-attachments/assets/4a18f53a-4044-44e8-a3cc-c2a25d804330" />
&nbsp;
&nbsp;

**/PROC/MODULES**

<img width="1344" height="315" alt="procmodules" src="https://github.com/user-attachments/assets/aa4bca5e-f983-4530-9a3e-5b7f043fd97b" />

&nbsp;
&nbsp;

**PROC RECONSTRUCT**


<img width="944" height="267" alt="proc1" src="https://github.com/user-attachments/assets/620088fa-c4e9-4c96-990c-94930ae61064" />
&nbsp;
&nbsp;

<img width="1280" height="88" alt="proc2" src="https://github.com/user-attachments/assets/bce8ba5f-133e-4a09-9cff-2194dc082853" />

&nbsp;
&nbsp;

**CRAWLERS**

<img width="606" height="300" alt="crawlers" src="https://github.com/user-attachments/assets/0a9a208d-1ad1-49bf-9011-5a1234c14990" />
&nbsp;
&nbsp;
<img width="1301" height="80" alt="crawlers2" src="https://github.com/user-attachments/assets/2ab8c265-8def-45e3-8694-b057344a1036" />

&nbsp;
&nbsp;

**RPM PACKAGE ANALYSIS**

<img width="580" height="505" alt="RPM_package_analysis" src="https://github.com/user-attachments/assets/8a067443-efd5-4253-868b-51b88d36670d" />
&nbsp;
&nbsp;

**DEB PACKAGE ANALYSIS**

<img width="602" height="61" alt="DEB_package_analysis" src="https://github.com/user-attachments/assets/791b69ba-5044-4422-abbb-0b5552d1d6b9" />
&nbsp;
&nbsp;

**TREE VIEW**

<img width="1237" height="310" alt="tree_view" src="https://github.com/user-attachments/assets/04b7bbb7-485b-4655-92c3-24041a5f0753" />


# LAP Portable/Standalone - Download

The already compiled LAP portable executable version can be downloaded here:


# Youtube Video Tutorials

Check out the "LAP - Linux Artifact Parser" Youtube channel for more comprehensive usage explanations:
