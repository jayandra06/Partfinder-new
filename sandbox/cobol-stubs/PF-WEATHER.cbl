       IDENTIFICATION DIVISION.
       PROGRAM-ID. PF-WEATHER.
      * Sandbox sensor config legacy interface sample
       DATA DIVISION.
       WORKING-STORAGE SECTION.
       01 WS-SENSOR-ID PIC 9(4) VALUE 4242.
       PROCEDURE DIVISION.
           DISPLAY "SANDBOX SENSOR" WS-SENSOR-ID
           STOP RUN.
