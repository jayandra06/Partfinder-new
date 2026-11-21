       IDENTIFICATION DIVISION.
       PROGRAM-ID. PF-SENSOR.
      * PartFinder sandbox — not deployed, not called from .NET/Node.
       DATA DIVISION.
       WORKING-STORAGE SECTION.
       01  WS-TEMP-C            PIC S9(5)V99 COMP-3.
       01  WS-RH-PCT            PIC 9(3)V9 COMP-3.
       01  WS-RADAR-XC          PIC V9(4) COMP-3.
       PROCEDURE DIVISION.
           MOVE 23.45 TO WS-TEMP-C
           MOVE 61.2 TO WS-RH-PCT
           MOVE 0.082 TO WS-RADAR-XC
           GOBACK.
       END PROGRAM PF-SENSOR.
