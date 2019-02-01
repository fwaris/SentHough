# Visual Analysis of Acceleration Sensor Data

This is a sample F# app that uses Xamarin and OpenCV to perform 'visual' processing of linear acceleration sensor data.

The main idea is to take a sample of sensor data and convert that into a picture in delay coordinates. Essentially we are freezing the window of sensor information (which could be 100's of elements) into a single composite picture.

The picture can then be processed using a Hough transform to extract structured information in the form of line equations - in polar coordinate - for the dominant lines discovered in the picture.

The line equations are a compression of the sensor data to a low dimensional space - you can choose to use the top n lines or pick all lines over some threshold. This compressed view can then be used for further processing such as classification or clustering, etc.

## Screen shot:
![Screen shot](http://github.com/fwaris/SentHough/Annotated_screen_shot.png)
