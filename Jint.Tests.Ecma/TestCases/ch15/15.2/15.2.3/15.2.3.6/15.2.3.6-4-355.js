/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-355.js
 * @description ES5 Attributes - property ([[Writable]] is false, [[Enumerable]] is true, [[Configurable]] is true) is enumerable
 */


function testcase() {
        var obj = {};

        Object.defineProperty(obj, "prop", {
            value: 2010,
            writable: false,
            enumerable: true,
            configurable: true
        });
        var propertyDefineCorrect = obj.hasOwnProperty("prop");
        var desc = Object.getOwnPropertyDescriptor(obj, "prop");
        for (var property in obj) {
            if (property === "prop") {
                return propertyDefineCorrect && desc.enumerable === true;
            }
        }
        return false;
    }
runTestCase(testcase);
